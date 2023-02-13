using System.Text.Json;
using DotNext.Net.Cluster;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http;
using DotNext.Net.Cluster.Messaging;
using Microsoft.AspNetCore.Connections;
using Raft3DockerClusterExample;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("CLUSTER_");

var services = builder.Services;

var logprefix = builder.Configuration.GetSection("HttpClusterMember").Get<Uri>()?.ToString()?.Split("_").Last()?.Replace('/', ':') ?? throw new MissingFieldException("HttpClusterMember");

builder.Logging.ClearProviders();

builder.Logging.AddSimpleConsole(o =>
{
   o.IncludeScopes = false;
   o.SingleLine = true;
   o.TimestampFormat = "HH:mm:ss.fff "+logprefix;
   o.UseUtcTimestamp = true;
});

void ConfigureRaftCluster()
{
   builder.JoinCluster();

   //services.AddSingleton<MetricsCollector, Metrics>();

   var me = builder.Configuration.GetSection("HttpClusterMember").Get<Uri>() ?? throw new MissingFieldException("HttpClusterMember");
   var knownMembers = builder.Configuration.GetSection("HttpClusterMembers").Get<Uri[]>() ?? throw new MissingFieldException("HttpClusterMembers");
   services.Configure<HttpClusterMemberConfiguration>(o =>
   {
      o.PublicEndPoint = me;
      //o.LowerElectionTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
      //o.UpperElectionTimeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
      o.Id = ClusterMemberId.FromEndPoint(new UriEndPoint(me));
      Console.WriteLine($"I am {me} {o.Id}");
      o.ColdStart = knownMembers.Length <= 1;
   });
   services.UseInMemoryConfigurationStorage(endPoints =>
   {
      foreach (var peerUri in knownMembers)
      {
         var peer = new UriEndPoint(peerUri);
         var peerId = ClusterMemberId.FromEndPoint(peer);
         Console.WriteLine($"peer {peer} id {peerId}");
         endPoints.Add(peerId, peer);
      }
   });

   services.ConfigureCluster<ClusterConfigurator>();
   services.AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>();

   services.AddSingleton<IInputChannel, TryMeHandler>();
   services.AddSingleton<IInputChannel, TryMeBroadcastHandler>();
}

ConfigureRaftCluster();

services.AddRazorPages();
services.AddControllers();

var app = builder.Build();

app.UseConsensusProtocolHandler()
   .RedirectToLeader("/api/leader");	

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

public class Metrics : HttpMetricsCollector
{
   private readonly ILogger<Metrics> _logger;

   public Metrics(ILogger<Metrics> logger)
   {
      _logger = logger;
   }

   public override void ReportResponseTime(TimeSpan value)
   {
      _logger.LogInformation($"ResponseTime {value.TotalMilliseconds:F2}ms");
      base.ReportResponseTime(value);
   }

   public override void MovedToCandidateState()
   {
      base.MovedToCandidateState();
   }

   public override void MovedToFollowerState()
   {
      base.MovedToFollowerState();
   }

   public override void MovedToLeaderState()
   {
      base.MovedToLeaderState();
   }

   public override void ReportBroadcastTime(TimeSpan value)
   {
      base.ReportBroadcastTime(value);
   }

   public override void ReportHeartbeat()
   {
      base.ReportHeartbeat();
   }
}