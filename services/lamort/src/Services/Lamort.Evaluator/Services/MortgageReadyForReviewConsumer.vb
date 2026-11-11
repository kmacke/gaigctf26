Imports Lamort.Shared.Messaging
Imports System.Threading
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Microsoft.Extensions.Options
Imports RabbitMQ.Client
Imports RabbitMQ.Client.Events

Namespace Services
    Public NotInheritable Class MortgageReadyForReviewConsumer
        Inherits BackgroundService

        Private ReadOnly _repository As ApplicationRepository
        Private ReadOnly _evaluator As MortgageEvaluator
        Private ReadOnly _options As RabbitMqOptions
        Private ReadOnly _logger As ILogger(Of MortgageReadyForReviewConsumer)

        Public Sub New(
            repository As ApplicationRepository,
            evaluator As MortgageEvaluator,
            options As IOptions(Of RabbitMqOptions),
            logger As ILogger(Of MortgageReadyForReviewConsumer))
            _repository = repository
            _evaluator = evaluator
            _options = options.Value
            _logger = logger
        End Sub

        Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
            Dim factory = New ConnectionFactory With {
                .HostName = _options.HostName,
                .UserName = _options.UserName,
                .Password = _options.Password
            }

            Using connection = factory.CreateConnection()
                Using channel = connection.CreateModel()
                    RabbitMqTopology.ConfigureConsumer(channel, _options)

                    Dim consumer = New EventingBasicConsumer(channel)
                    AddHandler consumer.Received, Async Sub(sender As Object, eventArgs As BasicDeliverEventArgs)
                                                      Dim message = RabbitMqTopology.Deserialize(Of MortgageApplicationReadyForReviewEvent)(eventArgs.Body)
                                                      If message Is Nothing Then
                                                          channel.BasicAck(eventArgs.DeliveryTag, False)
                                                          Return
                                                      End If

                                                      Try
                                                          Dim application = Await _repository.LoadAsync(message.MortgageApplicationId, stoppingToken)
                                                          If application Is Nothing Then
                                                              _logger.LogWarning("Mortgage {MortgageApplicationId} was not found for evaluation.", message.MortgageApplicationId)
                                                              channel.BasicAck(eventArgs.DeliveryTag, False)
                                                              Return
                                                          End If

                                                          Dim result = _evaluator.Evaluate(application)
                                                          Await _repository.SaveEvaluationAsync(application.Id, result, stoppingToken)
                                                          _logger.LogInformation(
                                                              "Evaluated mortgage {MortgageApplicationId} for user {UserId}: {Decision}. {Reason}",
                                                              application.Id,
                                                              application.UserId,
                                                              If(result.Approved, "YES", "NO"),
                                                              result.Reason)

                                                          channel.BasicAck(eventArgs.DeliveryTag, False)
                                                      Catch ex As Exception
                                                          _logger.LogError(ex, "Failed evaluating mortgage {MortgageApplicationId}", message.MortgageApplicationId)
                                                          channel.BasicNack(eventArgs.DeliveryTag, False, False)
                                                      End Try
                                                  End Sub

                    channel.BasicConsume(_options.QueueName, autoAck:=False, consumer:=consumer)
                    Await Task.Delay(Timeout.Infinite, stoppingToken)
                End Using
            End Using
        End Function
    End Class
End Namespace
