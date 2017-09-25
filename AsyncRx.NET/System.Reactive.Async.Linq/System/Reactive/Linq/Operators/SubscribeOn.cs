﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information. 

using System.Reactive.Concurrency;
using System.Reactive.Disposables;

namespace System.Reactive.Linq
{
    // REVIEW: Consider more fine-grained control over subscribe and dispose schedulers.

    partial class AsyncObservable
    {
        public static IAsyncObservable<TSource> SubscribeOn<TSource>(this IAsyncObservable<TSource> source, IAsyncScheduler scheduler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (scheduler == null)
                throw new ArgumentNullException(nameof(scheduler));

            return Create<TSource>(async observer =>
            {
                var m = new SingleAssignmentAsyncDisposable();
                var d = new SerialAsyncDisposable();

                await d.AssignAsync(m).ConfigureAwait(false);

                var scheduled = await scheduler.ScheduleAsync(async ct =>
                {
                    var subscription = await source.SubscribeSafeAsync(observer).RendezVous(scheduler, ct);

                    var scheduledDispose = AsyncDisposable.Create(async () =>
                    {
                        await scheduler.ScheduleAsync(async _ =>
                        {
                            await subscription.DisposeAsync().RendezVous(scheduler, ct);
                        }).ConfigureAwait(false);
                    });

                    await d.AssignAsync(scheduledDispose).RendezVous(scheduler, ct);
                }).ConfigureAwait(false);

                await m.AssignAsync(scheduled).ConfigureAwait(false);

                return d;
            });
        }
    }
}