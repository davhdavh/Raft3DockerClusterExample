using System.Diagnostics;
using DotNext.Net;
using DotNext.Net.Cluster;
using DotNext.Net.Cluster.Consensus.Raft;

namespace Raft3DockerClusterExample;

public class ClusterConfigurator : IClusterMemberLifetime
{
   private readonly ILogger<ClusterConfigurator> _logger;

   public ClusterConfigurator(ILogger<ClusterConfigurator> logger)
   {
      _logger = logger;
   }

   internal void LeaderChanged(ICluster cl, IClusterMember? leader)
   {
      if (cl is not IRaftCluster cluster)
      {
         Debug.Assert(cl is IRaftCluster);
         return;
      }
      var term = cluster.Term;
      var timeout = cluster.ElectionTimeout;
      _logger.LogInformation(leader is null
         ? "Consensus cannot be reached"
         : $"New cluster leader is elected. Leader address is {leader.EndPoint}.");
      _logger.LogInformation($"Term of local cluster member is {term}. Election timeout {timeout}");
   }

   public void OnStart(IRaftCluster cluster, IDictionary<string, string> metadata)
   {
      cluster.LeaderChanged += LeaderChanged;
      cluster.PeerDiscovered += ClusterOnPeerDiscovered;
      cluster.PeerGone += ClusterOnPeerGone;
   }

   private void ClusterOnPeerGone(IPeerMesh arg1, PeerEventArgs arg2)
   {
      _logger.LogInformation($"Peer gone: {arg2.PeerAddress}");
   }

   private void ClusterOnPeerDiscovered(IPeerMesh arg1, PeerEventArgs arg2)
   {
      _logger.LogInformation($"Peer discovered: {arg2.PeerAddress}");
   }

   public void OnStop(IRaftCluster cluster)
   {
      cluster.LeaderChanged -= LeaderChanged;
      cluster.PeerDiscovered -= ClusterOnPeerDiscovered;
      cluster.PeerGone -= ClusterOnPeerGone;
   }
}