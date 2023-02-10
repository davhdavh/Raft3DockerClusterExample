using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotNext.Buffers;

namespace DotNext.Net.Cluster.Messaging;

/// <summary>
/// Helper base class to implement a two way message handler that gets called via <see cref="JsonMessage{TIn}"/>
/// </summary>
/// <example>
/// <code>
/// public class ExampleHandler : JsonMessageHandler&lt;ExampleDto, ExampleDto, ExampleHandler&gt;, INameOfMessageHandler
/// {
///    private readonly ILogger&lt;ExampleHandler&gt; _logger;
///    public ExampleHandler(ILogger&lt;ExampleHandler&gt; logger) =&gt; _logger = logger;
///
///    public override Task&lt;ExampleDto&gt; OnMessage(ExampleDto message, CancellationToken token)
///    {
///       _logger.LogInformation($"Got {message.MyCustomValue}");
///       return Task.FromResult&lt;ExampleDto&gt;(new("Got:" + message.MyCustomValue));
///    }
/// 
///    public static string Name =&gt; nameof(ExampleHandler);
/// }
/// </code>
/// and in Program.cs: <code>services.AddSingleton&lt;IInputChannel, ExampleHandler&gt;();</code>
/// </example>
/// <typeparam name="TIn">The instance type the message handler accepts. It must implement <see cref="IJsonMessageSerializable{TIn}"/></typeparam>
/// <typeparam name="TOut">The instance type the message handler will return. It must implement <see cref="IJsonMessageSerializable{TOut}"/></typeparam>
/// <typeparam name="TSelf">Implementing class that also implements <see cref="INameOfMessageHandler"/></typeparam>
public abstract class JsonMessageHandler<TIn, TOut, TSelf> : IInputChannel
   where TIn : IJsonMessageSerializable<TIn>
   where TOut : IJsonMessageSerializable<TOut>
   where TSelf : JsonMessageHandler<TIn, TOut, TSelf>, INameOfMessageHandler
{
   /// <summary>
   /// Process the message
   /// </summary>
   /// <param name="message">The message sent from <see cref="Messenger.SendJsonMessageAsync{TIn,TOut}" /></param>
   /// <param name="token">CancellationToken</param>
   /// <returns>Instance of <see cref="TOut"/></returns>
   public abstract Task<TOut> OnMessage(TIn message, CancellationToken token);

   /// <summary>
   /// Only process the two way messages with the proper name
   /// </summary>
   bool IInputChannel.IsSupported(string messageName, bool oneWay) => !oneWay && TSelf.Name == messageName;

   /// <summary>
   /// Handle serialization
   /// </summary>
   async Task<IMessage> IInputChannel.ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token)
   {
      var inValue = TIn.TypeInfo is not null
         ? await JsonMessage<TIn>.FromJsonAsync(message, TIn.TypeInfo, TIn.Allocator, token) ?? throw new("Invalid payload")
         : await JsonMessage<TIn>.FromJsonAsync(message, TIn.Options, TIn.Allocator, token) ?? throw new("Invalid payload");

      return new JsonMessage<TOut>("Does it matter?!?", await OnMessage(inValue, token)){Options = TOut.Options, TypeInfo = TOut.TypeInfo};
   }

   Task IInputChannel.ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token) => throw new UnreachableException();

   /// <summary>
   /// Make a call to the Remote service on <see cref="leader"/>
   /// </summary>
   /// <param name="leader">Machine to call</param>
   /// <param name="message">Message to send</param>
   /// <param name="token">CancellationToken</param>
   /// <returns></returns>
   public static Task<TOut?> RemoteCallAsync(ISubscriber leader, TIn message, CancellationToken token = default)
   {
      ValueTask<TOut?> ResponseReader(IMessage x, CancellationToken y) => TOut.TypeInfo is not null
         ? JsonMessage<TOut>.FromJsonAsync(x, TOut.TypeInfo, TOut.Allocator, token)
         : JsonMessage<TOut>.FromJsonAsync(x, TOut.Options, TOut.Allocator, token);

      return leader.SendMessageAsync(
         new JsonMessage<TIn>(TSelf.Name, message) { Options = TIn.Options, TypeInfo = TIn.TypeInfo }, ResponseReader,
         token);
   }
}

/// <summary>
/// Helper base class to implement a one way message handler that gets called via <see cref="JsonMessage{TIn}"/>
/// </summary>
/// <example>
/// <code>
/// public class ExampleBroadcastHandler : JsonMessageHandler&lt;ExampleDto, ExampleBroadcastHandler&gt;, INameOfMessageHandler
/// {
///    private readonly ILogger&lt;ExampleBroadcastHandler&gt; _logger;
///    public ExampleBroadcastHandler(ILogger&lt;ExampleBroadcastHandler&gt; logger) =&gt; _logger = logger;
/// 
///    public override Task OnMessage(ExampleDto message, CancellationToken token)
///    {
///       _logger.LogInformation($"Got Broadcast {message.MyCustomValue}");
///       return Task.CompletedTask;
///    }
/// 
///    public static string Name =&gt; nameof(ExampleBroadcastHandler);
/// }
/// </code>
/// and in Program.cs: <code>services.AddSingleton&lt;IInputChannel, ExampleBroadcastHandler&gt;();</code>
/// </example>
/// <typeparam name="TIn">The instance type the message handler accepts</typeparam>
/// <typeparam name="TSelf">Implementing class that also implements <see cref="INameOfMessageHandler"/></typeparam>
public abstract class JsonMessageHandler<TIn, TSelf> : IInputChannel
   where TIn : IJsonMessageSerializable<TIn>
   where TSelf : JsonMessageHandler<TIn, TSelf>, INameOfMessageHandler
{
   /// <summary>
   /// Process the message
   /// </summary>
   /// <param name="message">The message sent from <see cref="RemoteCallAsync" /> or <see cref="BroadcastAsync"/></param>
   /// <param name="token">CancellationToken</param>
   public abstract Task OnMessage(TIn message, CancellationToken token);

   /// <summary>
   /// Only process the one way messages with the proper name
   /// </summary>
   bool IInputChannel.IsSupported(string messageName, bool oneWay) => oneWay && TSelf.Name == messageName;

   Task<IMessage> IInputChannel.ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token) => throw new UnreachableException();

   /// <summary>
   /// Handle serialization
   /// </summary>
   async Task IInputChannel.ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token)
   {
      var inValue = TIn.TypeInfo is not null
         ? await JsonMessage<TIn>.FromJsonAsync(signal, TIn.TypeInfo, TIn.Allocator, token) ?? throw new("Invalid payload")
         : await JsonMessage<TIn>.FromJsonAsync(signal, TIn.Options, TIn.Allocator, token) ?? throw new("Invalid payload");
      await OnMessage(inValue, token);
   }

   /// <summary>
   /// Make a call to the Remote service on <see cref="leader"/>
   /// </summary>
   /// <param name="leader">Machine to call</param>
   /// <param name="message">Message to send</param>
   /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
   /// <param name="token">CancellationToken</param>
   public static Task RemoteCallAsync(ISubscriber leader, TIn message, bool requiresConfirmation = true, CancellationToken token = default) => 
      leader.SendSignalAsync(new JsonMessage<TIn>(TSelf.Name, message) { Options = TIn.Options, TypeInfo = TIn.TypeInfo }, requiresConfirmation, token);

   /// <summary>
   /// Send message to all services in cluster except current machine
   /// </summary>
   /// <param name="cluster">Machine to call</param>
   /// <param name="message">Message to send</param>
   /// <param name="requiresConfirmation"><see langword="true"/> to wait for confirmation of delivery from receiver; otherwise, <see langword="false"/>.</param>
   public static Task BroadcastAsync(IMessageBus cluster, TIn message, bool requiresConfirmation = true) => 
      cluster.SendBroadcastSignalAsync(new JsonMessage<TIn>(TSelf.Name, message) { Options = TIn.Options, TypeInfo = TIn.TypeInfo }, requiresConfirmation);
}

/// <summary>
/// Implement in DTO classes to inform other classes how to handle serialization of the DTO to json
/// </summary>
/// <example>
/// <code>
/// public partial class ExampleDto : IJsonMessageSerializable&lt;ExampleDto&gt;
/// {
///    public string MyCustomValue { get; set; }
/// 
///    public ExampleDto(string myCustomValue) =&gt; MyCustomValue = myCustomValue;
/// 
///    public static JsonSerializerOptions? Options =&gt; null;
///    public static JsonTypeInfo&lt;ExampleDto&gt;? TypeInfo =&gt; MyJsonContext.Default.ExampleDto;
///    public static MemoryAllocator&lt;byte&gt;? Allocator =&gt; null;
///    
///    [JsonSerializable(typeof(ExampleDto))] private partial class MyJsonContext : JsonSerializerContext {}
/// }
/// </code>
/// </example>
/// <typeparam name="T">The DTO class itself</typeparam>
public interface IJsonMessageSerializable<T> where T: IJsonMessageSerializable<T>
{
   /// <summary>
   /// Optional set the serializer. Mutually exclusive to <see cref="TypeInfo"/>
   /// </summary>
   static abstract JsonSerializerOptions? Options { get; }
   /// <summary>
   /// Optional set the serializer. Mutually exclusive to <see cref="Options"/>
   /// </summary>
   static abstract JsonTypeInfo<T>? TypeInfo { get; }
   /// <summary>
   /// Optional set the MemoryAllocator.
   /// </summary>
   static abstract MemoryAllocator<byte>? Allocator { get; }
}

/// <summary>
/// Implement in class that inherits from <see cref="JsonMessageHandler{TIn, TSelf}"/> or <see cref="JsonMessageHandler{TIn, TOut, TSelf}"/>
/// </summary>
public interface INameOfMessageHandler
{
   /// <summary>
   /// Name that message handler handles
   /// </summary>
   static abstract string Name { get; }
}
