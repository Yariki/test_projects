using System.Collections.Concurrent;

internal class Program
{
        static object lockObject = new();
        static ConcurrentQueue<string> queue = new();

    private static void Main(string[] args)
    {
        CancellationTokenSource cancellationTokenSource = new();
        
        var producer = new Thread(Produce);
        var consumer = new Thread(Consume);
        producer.Start(cancellationTokenSource.Token);
        consumer.Start(cancellationTokenSource.Token);

        producer.Join();
        cancellationTokenSource.Cancel();
        lock(lockObject)
            Monitor.Pulse(lockObject);
        consumer.Join();
    }

    private static void Produce(object state)
    {
        CancellationToken token = (CancellationToken)state;

        for(int i = 1; i < 20; i++)
        {
            lock(lockObject)
            {
                queue.Enqueue($"Parameter {i}");
                Monitor.Pulse(lockObject);
                Thread.Sleep(250);
            }
        }
        Console.WriteLine("Exit Prodcucer...");
    }

    private static void Consume(object state)
    {
        CancellationToken token = (CancellationToken)state;

        while(!token.IsCancellationRequested)
        {
            lock(lockObject)
            {
                Monitor.Wait(lockObject);
                queue.TryDequeue(out var str);

                Console.WriteLine(str);
            }        
        }
        Console.WriteLine("Exit COnsumer...");
    }
}