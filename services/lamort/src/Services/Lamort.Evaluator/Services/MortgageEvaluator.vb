Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.Json

Namespace Services
    Public NotInheritable Class MortgageEvaluator
        Private Shared ReadOnly PerlJsonOptions As New JsonSerializerOptions With {
            .PropertyNameCaseInsensitive = True
        }

        Public Shared Function Evaluate(application As MortgageApplicationSnapshot) As EvaluationResult
            Dim workingDirectory = Path.Combine(Path.GetTempPath(), "lamort-evaluator", Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(workingDirectory)

            Try
                Dim scriptPath = Path.Combine(workingDirectory, "evaluate.pl")

                ' ricks eval algorithm
                ' does the job more or less
                Dim builder As New StringBuilder()
                builder.AppendLine("use strict;")
                builder.AppendLine("use warnings;")
                builder.AppendLine("use JSON::PP qw(encode_json);")
                builder.AppendLine()
                builder.AppendLine("my $score = 0;")
                ' SECURITY: amounts are user-supplied free text. Parse to a number and emit
                ' only the invariant-culture numeric literal, never the raw string, so it
                ' cannot inject Perl code.
                For Each income In application.Incomes
                    If income <> "" Then
                        builder.AppendLine($"$score += 5 * {ToNumericLiteral(income)};")
                    End If
                Next
                For Each asset In application.Assets
                    If asset <> "" Then
                        builder.AppendLine($"$score += {ToNumericLiteral(asset)};")
                    End If
                Next
                For Each debt In application.Debts
                    If debt <> "" Then
                        builder.AppendLine($"$score -= 2 * {ToNumericLiteral(debt)};")
                    End If
                Next
                If application.HomePriceMax.HasValue Then
                    builder.AppendLine($"my $home_price = {application.HomePriceMax.Value.ToString(CultureInfo.InvariantCulture)};")
                Else
                    builder.AppendLine("my $home_price = undef;")
                End If
                builder.AppendLine("my $required_score = 2;")
                builder.AppendLine("if (defined $home_price) {")
                builder.AppendLine("    if ($home_price <= 50000) {")
                builder.AppendLine("        $required_score = 2;")
                builder.AppendLine("    } elsif ($home_price >= 2000000) {")
                builder.AppendLine("        $required_score = 4;")
                builder.AppendLine("    } else {")
                builder.AppendLine("        $required_score = 2 + (($home_price - 50000) * 2 / 1950000);")
                builder.AppendLine("    }")
                builder.AppendLine("}")
                builder.AppendLine("my $approved = $score >= $required_score;")
                builder.AppendLine()
                builder.AppendLine("my $reason = $approved")
                builder.AppendLine("    ? 'Approved! Keep it real... estate!'")
                builder.AppendLine("    : 'Denied! Better luck next time.';")
                builder.AppendLine()
                builder.AppendLine("print encode_json({ approved => $approved ? JSON::PP::true() : JSON::PP::false(), reason => $reason });")
                Dim script = builder.ToString()

                File.WriteAllText(scriptPath, script)

                Dim startInfo = New ProcessStartInfo("perl") With {
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False
                }
                startInfo.ArgumentList.Add(scriptPath)

                Dim process As Process = Process.Start(startInfo)
                Using process
                    If process Is Nothing Then
                        Throw New InvalidOperationException("Failed to start process.")
                    End If

                    Dim standardOutput = process.StandardOutput.ReadToEnd().Trim()
                    Dim standardError = process.StandardError.ReadToEnd()
                    process.WaitForExit()

                    If process.ExitCode <> 0 Then
                        Throw New InvalidOperationException($"Evaluation failed with exit code {process.ExitCode}: {standardError}")
                    End If

                    Dim result = JsonSerializer.Deserialize(Of PerlEvaluationOutput)(standardOutput, PerlJsonOptions)
                    If result Is Nothing OrElse String.IsNullOrWhiteSpace(result.Reason) Then
                        Throw New InvalidOperationException($"Evaluation returned an invalid result. Output: {standardOutput}")
                    End If

                    Return New EvaluationResult(result.Approved, result.Reason)
                End Using
            Finally
                Try
                    Directory.Delete(workingDirectory, True)
                Catch ex As IOException
                    ' best-effort cleanup
                End Try
            End Try
        End Function

        Private Shared Function ToNumericLiteral(raw As String) As String
            Dim value As Decimal
            Dim cleaned = raw.Trim().Replace("$", "").Replace(",", "")
            If Not Decimal.TryParse(cleaned, NumberStyles.Number Or NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, value) Then
                Throw New InvalidOperationException("Amount is not a valid number.")
            End If
            Return value.ToString(CultureInfo.InvariantCulture)
        End Function
    End Class

    Public NotInheritable Class PerlEvaluationOutput
        Public Property Approved As Boolean

        Public Property Reason As String
    End Class

    Public NotInheritable Class EvaluationResult
        Public Sub New(approved As Boolean, reason As String)
            Me.Approved = approved
            Me.Reason = reason
        End Sub

        Public ReadOnly Property Approved As Boolean

        Public ReadOnly Property Reason As String
    End Class
End Namespace
