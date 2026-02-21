using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Avalonia.DBus;

internal static class ChannelExtensions
{
    internal static ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
    {
        var output = Channel.CreateUnbounded<T>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _ = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource();

            try
            {
                var tasks = inputs.Select(async input =>
                {
                    await foreach (var item in input.ReadAllAsync(cts.Token))
                        await output.Writer.WriteAsync(item, cts.Token);
                });

                await Task.WhenAll(tasks);
                output.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                await cts.CancelAsync();
                output.Writer.TryComplete(ex);
            }
        });

        return output.Reader;
    }

    internal static ChannelReader<TOutput> Select<TInput, TOutput>(
        ChannelReader<TInput> input,
        Func<TInput, TOutput> selector)
    {
        var output = Channel.CreateUnbounded<TOutput>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true
            });

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in input.ReadAllAsync())
                    await output.Writer.WriteAsync(selector(item));

                output.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                output.Writer.TryComplete(ex);
            }
        });

        return output.Reader;
    }
}
