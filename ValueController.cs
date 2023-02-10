using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNext.Buffers;
using DotNext.Net.Cluster.Consensus.Raft.Http;
using DotNext.Net.Cluster.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;

namespace Raft3DockerClusterExample;

[ApiController]
[Route("api/[controller]/[action]")]
[AllowAnonymous]
public class ValueController : ControllerBase
{
   private readonly ILogger<ValueController> _logger;
   private IRaftHttpCluster ClusterInfo { get; }

   public ValueController(IRaftHttpCluster clusterInfo, ILogger<ValueController> logger)
   {
      _logger = logger;
      ClusterInfo = clusterInfo;
   }

   public async Task<ActionResult> BroadCast()
   {
      await TryMeBroadcastHandler.BroadcastAsync(ClusterInfo, new("whatever"));
      return Ok();
   }

   public async Task<ActionResult<string>> FullInfo(CancellationToken token)
   {
      _logger.LogInformation("FullInfo");
      var leader = await EnsureLeaderIsElected(token);
      if (leader is null) return Problem(detail: "No leader");

      var throughMessaging = await SendThroughTwoWayMessaging(leader, token);

      var throughDirectHttpClient = await SendThroughDirectHttpClient(leader, token);

      var throughSelfHttpClient = await SendThroughSelfHttpClient(token);

      var throughLocalhost = await SendThroughLocalhost(token);

      return $"""
         Leader address is {leader.EndPoint}.
         Current address is {ClusterInfo.LocalMemberAddress}.
         Info from leader through the known url {throughDirectHttpClient}
         Info from leader through the local url {throughSelfHttpClient}
         Info from leader through localhost     {throughLocalhost}
         Info from Leader via messaging         {throughMessaging}
         """;

   }

   private static async Task<string> SendThroughLocalhost(CancellationToken token)
   {
      var client = new HttpClient(new HttpClientHandler
         { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
      client.BaseAddress = new Uri("https://localhost");
      var message = await client.GetAsync("/api/leader", token);
      message.EnsureSuccessStatusCode();
      return await message.Content.ReadAsStringAsync(token);
   }

   private async Task<string> SendThroughSelfHttpClient(CancellationToken token)
   {
      var client = new HttpClient(new HttpClientHandler
         { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
      client.BaseAddress = ClusterInfo.LocalMemberAddress;
      var message = await client.GetAsync("/api/leader", token);
      message.EnsureSuccessStatusCode();
      return await message.Content.ReadAsStringAsync(token);
   }

   private static async Task<string> SendThroughDirectHttpClient(ISubscriber leader, CancellationToken token)
   {
      var client = new HttpClient(new HttpClientHandler
         { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
      client.BaseAddress = ((UriEndPoint)leader.EndPoint).Uri;
      var message = await client.GetAsync("/api/Value/current", token);
      message.EnsureSuccessStatusCode();
      return await message.Content.ReadAsStringAsync(token);
   }

   private static async Task<string> SendThroughTwoWayMessaging(ISubscriber leader, CancellationToken token)
   {
      var response = await TryMeHandler.RemoteCallAsync(leader, new ("hest"), token);
      if (response == null) throw new ArgumentNullException(nameof(response));
      return response.MyCustomValue;
   }

   private async ValueTask<ISubscriber?> EnsureLeaderIsElected(CancellationToken token)
   {
      var leader = ClusterInfo.Leader;
      if (leader == null)
      {
         await ClusterInfo.WaitForLeaderAsync(TimeSpan.FromSeconds(1), token);
         leader = ClusterInfo.Leader;
      }
      return leader;
   }

   public ActionResult<string> Current()
   {
      return $"Leader address is {ClusterInfo.Leader?.EndPoint}. Current address is {ClusterInfo.LocalMemberAddress}.";
   }
}

public partial class TestMessagingDto : IJsonMessageSerializable<TestMessagingDto>
{
   public string MyCustomValue { get; set; }

   public TestMessagingDto(string myCustomValue) => MyCustomValue = myCustomValue;

   public static JsonSerializerOptions? Options => null;
   public static JsonTypeInfo<TestMessagingDto>? TypeInfo => MyJsonContext.Default.TestMessagingDto;
   public static MemoryAllocator<byte>? Allocator => null;

   
   [JsonSerializable(typeof(TestMessagingDto))]
   private partial class MyJsonContext : JsonSerializerContext
   {
   }
}

