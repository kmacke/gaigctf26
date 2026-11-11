Imports System.Data
Imports System.Threading
Imports Microsoft.Extensions.Configuration
Imports Npgsql

Namespace Services
    Public NotInheritable Class ApplicationRepository
        Private ReadOnly _configuration As IConfiguration

        Public Sub New(configuration As IConfiguration)
            _configuration = configuration
        End Sub

        Public Async Function LoadAsync(mortgageApplicationId As Guid, cancellationToken As CancellationToken) As Task(Of MortgageApplicationSnapshot)
            Dim connectionString = _configuration.GetConnectionString("LamortDb")

            If String.IsNullOrWhiteSpace(connectionString) Then
                Throw New InvalidOperationException("ConnectionStrings:LamortDb is not configured.")
            End If

            Using connection = New NpgsqlConnection(connectionString)
                Await connection.OpenAsync(cancellationToken)
                Return Await LoadApplicationAsync(connection, mortgageApplicationId, cancellationToken)
            End Using
        End Function

        Public Async Function SaveEvaluationAsync(mortgageApplicationId As Guid, result As EvaluationResult, cancellationToken As CancellationToken) As Task
            Dim connectionString = _configuration.GetConnectionString("LamortDb")

            If String.IsNullOrWhiteSpace(connectionString) Then
                Throw New InvalidOperationException("ConnectionStrings:LamortDb is not configured.")
            End If

            Const sql As String =
                "INSERT INTO mortgage.evaluations (mortgage_application_id, approved, reason, evaluated_at) " &
                "VALUES (@id, @approved, @reason, @evaluatedAt) " &
                "ON CONFLICT (mortgage_application_id) DO UPDATE SET " &
                "approved = EXCLUDED.approved, " &
                "reason = EXCLUDED.reason, " &
                "evaluated_at = EXCLUDED.evaluated_at;"

            Using connection = New NpgsqlConnection(connectionString)
                Await connection.OpenAsync(cancellationToken)
                Await EnsureEvaluationsTableAsync(connection, cancellationToken)

                Using command = New NpgsqlCommand(sql, connection)
                    command.Parameters.AddWithValue("@id", mortgageApplicationId)
                    command.Parameters.AddWithValue("@approved", result.Approved)
                    command.Parameters.AddWithValue("@reason", result.Reason)
                    command.Parameters.AddWithValue("@evaluatedAt", DateTimeOffset.UtcNow)
                    Await command.ExecuteNonQueryAsync(cancellationToken)
                End Using
            End Using
        End Function

        Private Shared Async Function EnsureEvaluationsTableAsync(connection As NpgsqlConnection, cancellationToken As CancellationToken) As Task
            Const sql As String =
                "CREATE SCHEMA IF NOT EXISTS mortgage;" &
                "CREATE TABLE IF NOT EXISTS mortgage.evaluations (" &
                "mortgage_application_id uuid PRIMARY KEY REFERENCES mortgage.applications (id) ON DELETE CASCADE, " &
                "approved boolean NOT NULL, " &
                "reason character varying(500) NOT NULL, " &
                "evaluated_at timestamp with time zone NOT NULL" &
                ");"

            Using command = New NpgsqlCommand(sql, connection)
                Await command.ExecuteNonQueryAsync(cancellationToken)
            End Using
        End Function

        Private Shared Async Function LoadApplicationAsync(connection As NpgsqlConnection, mortgageApplicationId As Guid, cancellationToken As CancellationToken) As Task(Of MortgageApplicationSnapshot)
            Dim incomeAmounts = Await LoadStringValuesAsync(
                connection,
                "SELECT amount FROM mortgage.incomes WHERE mortgage_application_id = @id ORDER BY sort_order;",
                mortgageApplicationId,
                cancellationToken)

            Dim assetAmounts = Await LoadStringValuesAsync(
                connection,
                "SELECT amount FROM mortgage.assets WHERE mortgage_application_id = @id ORDER BY sort_order;",
                mortgageApplicationId,
                cancellationToken)

            Dim debtAmounts = Await LoadStringValuesAsync(
                connection,
                "SELECT amount::text FROM mortgage.debts WHERE mortgage_application_id = @id ORDER BY sort_order;",
                mortgageApplicationId,
                cancellationToken)

            Const sql As String =
                "SELECT id, user_id, has_co_applicants, social_security_number, home_price_max " &
                "FROM mortgage.applications " &
                "WHERE id = @id;"

            Using command = New NpgsqlCommand(sql, connection)
                command.Parameters.AddWithValue("@id", mortgageApplicationId)

                Using reader = Await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken)
                    If Not Await reader.ReadAsync(cancellationToken) Then
                        Return Nothing
                    End If

                    Return New MortgageApplicationSnapshot(
                        reader.GetGuid(0),
                        reader.GetString(1),
                        reader.GetBoolean(2),
                        If(reader.IsDBNull(3), Nothing, reader.GetString(3)),
                        If(reader.IsDBNull(4), CType(Nothing, Decimal?), reader.GetDecimal(4)),
                        incomeAmounts,
                        assetAmounts,
                        debtAmounts)
                End Using
            End Using
        End Function

        Private Shared Async Function LoadStringValuesAsync(connection As NpgsqlConnection, sql As String, mortgageApplicationId As Guid, cancellationToken As CancellationToken) As Task(Of String())
            Using command = New NpgsqlCommand(sql, connection)
                command.Parameters.AddWithValue("@id", mortgageApplicationId)

                Using reader = Await command.ExecuteReaderAsync(cancellationToken)
                    Dim values As New List(Of String)()

                    While Await reader.ReadAsync(cancellationToken)
                        If reader.IsDBNull(0) Then
                            values.Add(String.Empty)
                        Else
                            values.Add(reader.GetValue(0).ToString())
                        End If
                    End While

                    Return values.ToArray()
                End Using
            End Using
        End Function
    End Class

    Public NotInheritable Class MortgageApplicationSnapshot
        Public Sub New(id As Guid, userId As String, hasCoApplicants As Boolean, socialSecurityNumber As String, homePriceMax As Decimal?, incomes As String(), assets As String(), debts As String())
            Me.Id = id
            Me.UserId = userId
            Me.HasCoApplicants = hasCoApplicants
            Me.SocialSecurityNumber = socialSecurityNumber
            Me.HomePriceMax = homePriceMax
            Me.Incomes = incomes
            Me.Assets = assets
            Me.Debts = debts
        End Sub

        Public ReadOnly Property Id As Guid

        Public ReadOnly Property UserId As String

        Public ReadOnly Property HasCoApplicants As Boolean

        Public ReadOnly Property SocialSecurityNumber As String

        Public ReadOnly Property HomePriceMax As Decimal?

        Public ReadOnly Property Incomes As String()

        Public ReadOnly Property Assets As String()

        Public ReadOnly Property Debts As String()
    End Class
End Namespace
