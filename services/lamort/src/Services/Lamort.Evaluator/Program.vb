Imports Lamort.Evaluator.Services
Imports Lamort.Shared.Messaging
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting

Module Program
    Sub Main(args As String())
        Dim builder = Host.CreateApplicationBuilder(args)

        builder.Services.Configure(Of RabbitMqOptions)(builder.Configuration.GetSection("RabbitMq"))
        builder.Services.AddSingleton(Of ApplicationRepository)()
        builder.Services.AddSingleton(Of MortgageEvaluator)()
        builder.Services.AddHostedService(Of MortgageReadyForReviewConsumer)()

        Dim app = builder.Build()
        app.Run()
    End Sub
End Module
