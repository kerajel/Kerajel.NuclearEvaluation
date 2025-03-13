namespace NuclearEvaluation.Kernel.Interfaces;

public interface IMessager
{
    Task PublishMessageAsync<T>(T message, string exchangeName, string routingKey);
}