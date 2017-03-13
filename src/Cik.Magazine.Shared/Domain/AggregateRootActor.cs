﻿using System;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence;
using Cik.Magazine.Shared.Messages;

namespace Cik.Magazine.Shared.Domain
{
    public class AggregateRootCreationParameters
    {
        public AggregateRootCreationParameters(Guid id, IActorRef projections, IActorRef processManager, int snapshotThreshold = 250)
        {
            Id = id;
            Projections = projections;
            ProcessManager = processManager;
            SnapshotThreshold = snapshotThreshold;
        }

        public Guid Id { get; }
        public IActorRef Projections { get; }
        public IActorRef ProcessManager { get; }
        public int SnapshotThreshold { get; }
    }

    public abstract class AggregateRootActor : PersistentActor, IEventSink
    {
        private readonly Guid _id;
        private readonly ILoggingAdapter _log;
        private readonly IActorRef _projections;
        private readonly IActorRef _processManager;
        private readonly int _snapshotThreshold;

        protected AggregateRootActor(AggregateRootCreationParameters parameters)
        {
            _id = parameters.Id;
            _projections = parameters.Projections;
            _processManager = parameters.ProcessManager;
            _snapshotThreshold = parameters.SnapshotThreshold;

            _log = Context.GetLogger();
        }

        public override string PersistenceId => $"{GetType().Name}-agg-{_id:n}".ToLowerInvariant();

        private long LastSnapshottedVersion { get; set; }

        void IEventSink.Publish(IEvent @event)
        {
            Persist(@event, e =>
            {
                Apply(e);
                _projections.Tell(@event);
                _processManager.Tell(@event);
                Self.Tell(SaveAggregate.Message); // save the snapshot if it is possible
            });
        }

        protected override bool ReceiveRecover(object message)
        {
            return message.Match()
                .With<RecoveryCompleted>(x => { _log.Debug("Recovered state to version {0}", LastSequenceNr); })
                .With<SnapshotOffer>(offer =>
                {
                    _log.Debug("State loaded from snapshot");
                    LastSnapshottedVersion = offer.Metadata.SequenceNr;
                    RecoverState(offer.Snapshot);
                })
                .With<IEvent>(x => Apply(x))
                .WasHandled;
        }

        protected override bool ReceiveCommand(object message)
        {
            return message.Match()
                .With<SaveAggregate>(x => Save())
                .With<SaveSnapshotSuccess>(success =>
                {
                    _log.Debug("Saved snapshot");
                    DeleteMessages(success.Metadata.SequenceNr);
                })
                .With<SaveSnapshotFailure>(failure => {
                    // handle snapshot save failure...
                })
                .With<ICommand>(command =>
                {
                    try
                    {
                        var handled = Handle(command);
                        Sender.Tell(new CommandResponse(handled));
                    }
                    catch (DomainException e)
                    {
                        Sender.Tell(e);
                    }
                }).WasHandled;
        }

        private bool Save()
        {
            if (LastSequenceNr - LastSnapshottedVersion >= _snapshotThreshold)
            {
                SaveSnapshot(GetState());
            }

            return true;
        }

        protected abstract bool Handle(ICommand command);
        protected abstract bool Apply(IEvent @event);
        protected abstract void RecoverState(object state);
        protected abstract object GetState();
    }
}