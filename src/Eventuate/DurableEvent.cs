﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Eventuate
{

    /// <summary>
    /// Provider API.
    /// 
    /// Event storage format. Fields `localLogId` and `localSequenceNr` differ among replicas, all other fields are not changed
    /// during event replication.                     
    /// </summary>
    public sealed class DurableEvent
    {
        public const string UndefinedEmittedId = "";
        public const string UndefinedLogId = "";
        public const long UndefinedSequenceNr = 0L;

        public DurableEvent(object payload, 
            string emitterId = null, 
            string emitterAggregateId = null, 
            ImmutableHashSet<string> customDestinationAggregateIds = null,
            DateTime systemTimestamp = default,
            VectorTime vectorTimestamp = null,
            string processId = null,
            string localLogId = null,
            long localSequenceNr = 0,
            string deliveryId = null,
            long? persistOnEventSequenceNr = null,
            EventId? persistOnEventId = null)
        {
            Payload = payload;
            EmitterId = emitterId ?? UndefinedEmittedId;
            EmitterAggregateId = emitterAggregateId;
            CustomDestinationAggregateIds = customDestinationAggregateIds ?? ImmutableHashSet<string>.Empty;
            SystemTimestamp = systemTimestamp;
            VectorTimestamp = vectorTimestamp ?? VectorTime.Zero;
            ProcessId = processId ?? UndefinedLogId;
            LocalLogId = localLogId ?? UndefinedLogId;
            LocalSequenceNr = localSequenceNr;
            DeliveryId = deliveryId;
            PersistOnEventSequenceNr = persistOnEventSequenceNr;
            PersistOnEventId = persistOnEventId;
            Id = new EventId(ProcessId, VectorTimestamp[ProcessId]);
        }

        /// <summary>
        /// Application-defined event.
        /// </summary>
        public object Payload { get; }

        /// <summary>
        /// Id of emitter (<see cref="EventsourcedActor"/> or <see cref="EventsourcedProcessor"/>).
        /// </summary>
        public string EmitterId { get; }

        /// <summary>
        /// Aggregate id of emitter (<see cref="EventsourcedActor"/> or <see cref="EventsourcedProcessor"/>). This is also
        /// the default routing destination of this event. If defined, the event is routed to event-
        /// sourced actors, views, writers and processors with a matching <see cref="AggregateId"/>. In any case,
        /// the event is routed to event-sourced actors, views, writers and processors with an undefined
        /// <see cref="AggregateId"/>.
        /// </summary>
        public string EmitterAggregateId { get; }

        /// <summary>
        /// Aggregate ids of additional, custom routing destinations. If non-empty, the event is
        /// additionally routed to event-sourced actors, views, writers and processors with a
        /// matching <see cref="AggregateId"/>.
        /// </summary>
        public ImmutableHashSet<string> CustomDestinationAggregateIds { get; }

        /// <summary>
        /// Wall-clock timestamp, generated by the source of concurrent activity that is identified by <see cref="ProcessId"/>.
        /// </summary>
        public DateTime SystemTimestamp { get; }

        /// <summary>
        /// Vector timestamp, generated by the source of concurrent activity that is identified by <see cref="ProcessId"/>.
        /// </summary>
        public VectorTime VectorTimestamp { get; }

        /// <summary>
        /// Id of the causality-tracking source of concurrent activity. This is the id of the local event log that
        /// initially wrote the event.
        /// </summary>
        public string ProcessId { get; }

        /// <summary>
        /// Id of the local event log.
        /// </summary>
        public string LocalLogId { get; }

        /// <summary>
        /// Sequence number in the local event log.
        /// </summary>
        public long LocalSequenceNr { get; }

        /// <summary>
        /// Delivery id chosen by an application that persisted this event with <see cref="ConfirmedDelivery.PersistConfirmation"/>.
        /// </summary>
        public string DeliveryId { get; }

        /// <summary>
        /// Sequence number of the event that caused the emission of this event in an event handler.
        /// Defined if an <see cref="EventsourcedActor"/> with a <see cref="PersistOnEvent"/> mixin emitted this event
        /// with `persistOnEvent`. Actually superseded by `persistOnEventId`, but still
        /// has to be maintained for backwards compatibility. It is required for confirmation
        /// of old [[com.rbmhtechnology.eventuate.PersistOnEvent.PersistOnEventRequest]]s from
        /// a snapshot that do not have [[com.rbmhtechnology.eventuate.PersistOnEvent.PersistOnEventRequest.persistOnEventId]]
        /// set.
        /// </summary>
        public long? PersistOnEventSequenceNr { get; }

        /// <summary>
        /// event id of the event that caused the emission of this event in an event handler.
        /// Defined if an  <see cref="EventsourcedActor"/> with a <see cref="PersistOnEvent"/> mixin emitted this event
        /// with `persistOnEvent`.
        /// </summary>
        public EventId? PersistOnEventId { get; }

        /// <summary>
        /// Unique event identifier.
        /// </summary>
        public EventId Id { get; }

        /// <summary>
        /// The default routing destination of this event is its <see cref="EmitterAggregateId"/>. If defined, the event is
        /// routed to event-sourced actors, views, writers and processors with a matching <see cref="AggregateId"/>. In any case, the event is
        /// routed to event-sourced actors, views, writers and processors with an undefined <see cref="AggregateId"/>.
        /// </summary>
        public string DefaultDestinationAggregateId => EmitterAggregateId;

        /// <summary>
        /// The union of <see cref="DestinationAggregateIds"/> and <see cref="CustomDestinationAggregateIds"/>.
        /// </summary>
        public ImmutableHashSet<string> DestinationAggregateIds =>
            DefaultDestinationAggregateId is null ? CustomDestinationAggregateIds : CustomDestinationAggregateIds.Add(DefaultDestinationAggregateId);

        /// <summary>
        /// Returns <c>true</c> if this event did not happen before or at the given <c>vectorTime</c>
        /// and passes the given replication <c>filter</c>.
        /// </summary>
        public bool IsReplicable(VectorTime vectorTime, ReplicationFilter filter) => !IsBefore(vectorTime) && filter.Invoke(this);

        /// <summary>
        /// Returns <c>true</c> if this event happened before or at the given <paramref name="vectorTime"/>.
        /// </summary>
        public bool IsBefore(VectorTime vectorTime) => VectorTimestamp <= vectorTime;

        /// <summary>
        /// Prepares the event for writing to an event log.
        /// </summary>
        internal DurableEvent Prepare(string logId, long sequenceNr, DateTime timestamp)
        {
            var id = ProcessId == UndefinedLogId ? logId : ProcessId;
            var vt = ProcessId == UndefinedLogId ? VectorTimestamp.SetLocalTime(logId, sequenceNr) : VectorTimestamp;
            return new DurableEvent(Payload, systemTimestamp: timestamp, vectorTimestamp: vt, processId: id, localLogId: logId, localSequenceNr: sequenceNr);
        }
    }

    /// <summary>
    /// Implemented by protocol messages that contain a <see cref="DurableEvent"/> sequence.
    /// </summary>
    public interface IDurableEventBatch
    {
        /// <summary>
        /// Event sequence size.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Event sequence.
        /// </summary>
        IEnumerable<DurableEvent> Events { get; }
    }

    /// <summary>
    /// Implemented by protocol messages whose event sequence can be updated.
    /// </summary>
    public interface IUpdateableEventBatch<TResult> : IDurableEventBatch
        where TResult : IUpdateableEventBatch<TResult>
    {
        /// <summary>
        /// Replaces this batch's events with the given <paramref name="events"/>.
        /// </summary>
        TResult Update(params DurableEvent[] events);
    }
}
