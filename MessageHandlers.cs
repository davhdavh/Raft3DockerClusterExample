using DotNext.Net.Cluster.Messaging;

namespace Raft3DockerClusterExample;

public class TryMeHandler : JsonMessageHandler<TestMessagingDto, TestMessagingDto, TryMeHandler>, INameOfMessageHandler
{
   private readonly ILogger<TryMeHandler> _logger;
   public TryMeHandler(ILogger<TryMeHandler> logger) => _logger = logger;

   public override Task<TestMessagingDto> OnMessage(TestMessagingDto message, CancellationToken token)
   {
      _logger.LogInformation($"Got {message.MyCustomValue}");
      return Task.FromResult<TestMessagingDto>(new("jens:" + message.MyCustomValue));
   }

   public static string Name => nameof(TryMeHandler);
}

public class TryMeBroadcastHandler : JsonMessageHandler<TestMessagingDto, TryMeBroadcastHandler>, INameOfMessageHandler
{
   private readonly ILogger<TryMeBroadcastHandler> _logger;
   public TryMeBroadcastHandler(ILogger<TryMeBroadcastHandler> logger) => _logger = logger;

   public override Task OnMessage(TestMessagingDto message, CancellationToken token)
   {
      _logger.LogInformation($"Got Broadcast {message.MyCustomValue}");
      return Task.CompletedTask;
   }

   public static string Name => nameof(TryMeBroadcastHandler);
}


