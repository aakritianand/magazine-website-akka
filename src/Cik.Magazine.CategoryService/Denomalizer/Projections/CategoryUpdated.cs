﻿using Akka.Actor;
using Akka.Event;
using Cik.Magazine.CategoryService.Denomalizer.Messages;
using Cik.Magazine.Shared;

namespace Cik.Magazine.CategoryService.Denomalizer.Projections
{
    public class CategoryUpdated : TypedActor, IHandle<Shared.Messages.Category.CategoryUpdated>
    {
        private readonly ILoggingAdapter _log;
        private readonly IActorRef _storage;

        public CategoryUpdated()
        {
            _storage = Context.ActorOf(Props.Create<NoSqlStorage>(), SystemData.CategoryStorageActor.Name);
            _log = Context.GetLogger();
        }

        public void Handle(Shared.Messages.Category.CategoryUpdated message)
        {
            _log.Info("CategoryUpdated is handled.");
            _storage.Tell(new UpdateCategory(message.AggregateId, message.Name));
        }
    }
}