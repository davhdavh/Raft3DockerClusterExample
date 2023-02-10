using DotNext.Net.Cluster.Consensus.Raft.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Raft3DockerClusterExample;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class LeaderController : ControllerBase
{
   private IRaftHttpCluster ClusterInfo { get; }

   public LeaderController(IRaftHttpCluster clusterInfo)
   {
      ClusterInfo = clusterInfo;
   }

   [HttpGet]
   public string CurrentLeader()
   {
      return $"Leader address is {ClusterInfo.Leader?.EndPoint}. Current address is {ClusterInfo.LocalMemberAddress}";
   }
}