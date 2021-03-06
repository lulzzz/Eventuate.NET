﻿#region copyright
// -----------------------------------------------------------------------
//  <copyright file="DurableEventProcessor.cs" company="Bartosz Sypytkowski">
//      Copyright (C) 2015-2019 Red Bull Media House GmbH <http://www.redbullmediahouse.com>
//      Copyright (C) 2019-2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using Akka;
using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Eventuate.Streams
{
    /// <summary>
    /// Stream-based alternative to <see cref="EventsourcedProcessor"/> and <see cref="StatefulProcessor"/>.
    /// </summary>
    public static class DurableEventProcessor
    {
        /// <summary>
        /// Creates an Akka.NET Streams stage that processes input <see cref="DurableEvent"/>s
        /// with stateless <paramref name="logic"/>, writes the processed events to <paramref name="eventLog"/> and emits the written
        /// <see cref="DurableEvent"/>s. The processor supports idempotent event processing by ignoring processed <see cref="DurableEvent"/>s
        /// that have already been written in the past to <paramref name="eventLog"/> which is determined by their `vectorTimestamp`s.
        /// Behavior of the processor can be configured with:
        /// 
        ///  - `eventuate.log.write-batch-size`. Maximum size of <see cref="DurableEvent"/> batches written to the event log. Events
        ///  are batched (with [[Flow.batch]]) if they are produced faster than this processor can process and write events.
        ///  - `eventuate.log.write-timeout`. Timeout for writing events to the event log. A write timeout or another write
        ///  failure causes this stage to fail.
        /// 
        /// This processor should be used in combination with one or more <see cref="DurableEventSource"/>s.
        /// </summary>
        /// <param name="id">A global unique writer id.</param>
        /// <param name="eventLog">Target event log.</param>
        /// <param name="logic">
        /// Stateless processing logic. Input is the <see cref="DurableEvent"/> input of this processor. The result
        /// must be a sequence of zero or more <see cref="DurableEvent"/> '''payloads'''. Restricting the processing
        /// logic to only read <see cref="DurableEvent"/> metadata allows the processor to correctly update these metadata
        /// before processed events are written to the target event log. The processing logic can be used to
        /// drop, transform and split <see cref="DurableEvent"/>s:
        /// 
        ///  - to drop an event, an empty sequence should be returned
        ///  - to transform an event, a sequence of length 1 should be returned
        ///  - to split an event, a sequence of length > 1 should be returned
        /// </param>
        public static Flow<DurableEvent, DurableEvent, NotUsed> StatelessProcessor<TOut>(string id, IActorRef eventLog, Func<DurableEvent, ImmutableArray<TOut>> logic, int batchSize = 64, TimeSpan? timeout = null) =>
            StatefullProcessor<Void, TOut>(id, eventLog, default, (s, e) => (s, logic(e)), batchSize, timeout);

        /// <summary>
        /// Creates an Akka.NET Streams stage that processes input
        /// <see cref="DurableEvent"/>s with stateful <paramref name="logic"/>, writes the processed events to <paramref name="eventLog"/> and emits the written
        /// <see cref="DurableEvent"/>s. The processor supports idempotent event processing by ignoring processed <see cref="DurableEvent"/>s
        /// that have already been written in the past to `eventLog` which is determined by their `vectorTimestamp`s.
        /// Behavior of the processor can be configured with:
        /// 
        ///  - `eventuate.log.write-batch-size`. Maximum size of <see cref="DurableEvent"/> batches written to the event log. Events
        ///  are batched (with [[Flow.batch]]) if they are produced faster than this processor can process and write events.
        ///  - `eventuate.log.write-timeout`. Timeout for writing events to the event log. A write timeout or another write
        ///  failure causes this stage to fail.
        /// 
        /// This processor should be used in combination with one or more <see cref="DurableEventSource"/>s.
        /// </summary>
        /// <param name="id">A global unique writer id.</param>
        /// <param name="eventLog">Target event log.</param>
        /// <param name="logic">
        /// Stateful processing logic. The state part of the input is either `zero` for the first stream element
        /// or the updated state from the previous processing step for all other stream elements. The event part
        /// of the input is the the <see cref="DurableEvent"/> input of this processor. The event part of the result must
        /// be a sequence of zero or more <see cref="DurableEvent"/> '''payloads'''. Restricting the processing logic to
        /// only read <see cref="DurableEvent"/> metadata allows the processor to correctly update these metadata before
        /// processed events are written to the target event log. The processing logic can be used to drop,
        /// transform and split <see cref="DurableEvent"/>s:
        /// 
        ///  - to drop an event, an empty sequence should be returned in the event part of the result
        ///  - to transform an event, a sequence of length 1 should be returned in the event part of the result
        ///  - to split an event, a sequence of length > 1 should be returned in the event part of the result
        /// </param>
        public static Flow<DurableEvent, DurableEvent, NotUsed> StatefullProcessor<TState, TOut>(string id, IActorRef eventLog, TState zero, Func<TState, DurableEvent, (TState, ImmutableArray<TOut>)> logic, int batchSize = 64, TimeSpan? timeout = null) =>
            Flow.FromGraph(Transformer(zero, logic))
                .Via(new BatchWriteStage(DurableEventWriter.ReplicationBatchWriter(id, eventLog, timeout ?? DurableEventWriter.DefaultWriteTimeout)))
                .SelectMany(e => e);

        private static IGraph<FlowShape<DurableEvent, ImmutableArray<DurableEvent>>, NotUsed> Transformer<TState, TOut>(TState zero, Func<TState, DurableEvent, (TState, ImmutableArray<TOut>)> logic)
        {
            var graph = GraphDsl.Create(b =>
            {
                var unzip = b.Add(new UnzipWith<DurableEvent, DurableEvent, DurableEvent>(e => Tuple.Create(e, e)));
                var zip = b.Add(new ZipWith<ImmutableArray<TOut>, DurableEvent, ImmutableArray<DurableEvent>>(
                    (payloads, e) => payloads.Select(p => new DurableEvent(p, e.EmitterId, e.EmitterAggregateId, e.CustomDestinationAggregateIds, e.SystemTimestamp, e.VectorTimestamp, e.ProcessId, e.LocalLogId, e.LocalSequenceNr, e.DeliveryId, e.PersistOnEventSequenceNr, e.PersistOnEventId)).ToImmutableArray()));

                var transform = b.Add(Flow.Create<DurableEvent>()
                    .Scan((zero, ImmutableArray<TOut>.Empty), ((TState, ImmutableArray<TOut>) t, DurableEvent p) => logic(t.Item1, p))
                    .Select(t => t.Item2)
                    .Skip(1));

                b.From(unzip.Out0).Via(transform).To(zip.In0);
                b.From(unzip.Out1).To(zip.In1);

                return new FlowShape<DurableEvent, ImmutableArray<DurableEvent>>(unzip.In, zip.Out);
            });

            return graph;
        }
    }
}
