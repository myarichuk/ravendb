﻿using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents.Replication;
using Sparrow.Json;
using Sparrow.Json.Parsing;

namespace Raven.Client.Server
{
    public class ExternalReplication : ReplicationNode, IDatabaseTask, IDynamicJsonValueConvertible
    {
        public long TaskId;
        public string Name;

        public static void RemoveWatcher(ref List<ExternalReplication> watchers, long taskId)
        {
            foreach (var watcher in watchers)
            {
                if (watcher.TaskId != taskId)
                    continue;
                watchers.Remove(watcher);
                return;
            }
        }

        public static void EnsureUniqueDbAndUrl(List<ExternalReplication> watchers, ExternalReplication watcher)
        {
            var dbName = watcher.Database;
            var url = watcher.Url;
            foreach (var w in watchers)
            {
                if (w.Database != dbName || w.Url != url)
                    continue;
                watchers.Remove(watcher);
                return;
            }
        }

        public static (HashSet<string> addDestinations, HashSet<string> removeDestinations) FindExternalConnectionChanges(
            List<ExternalReplication> oldDestinations, List<ExternalReplication> newDestinations)
        {
            var oldList = new List<string>(); // TODO: Remove the allocation
            var newList = new List<string>(); // TODO: Remove the allocation

            if (oldDestinations != null)
            {
                oldList.AddRange(oldDestinations.Select(s => s.Url + "@" + s.Database));
            }
            if (newDestinations != null)
            {
                newList.AddRange(newDestinations.Select(s => s.Url + "@" + s.Database));
            }

            var addDestinations = new HashSet<string>(newList);
            var removeDestinations = new HashSet<string>(oldList);

            foreach (var destination in newList)
            {
                if (removeDestinations.Contains(destination))
                {
                    removeDestinations.Remove(destination);
                    addDestinations.Remove(destination);
                }
            }

            return (addDestinations, removeDestinations);
        }


        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(TaskId)] = TaskId;
            json[nameof(Name)] = Name;
            return json;
        }

        public override string FromString()
        {
            return $"[{Database} @ {Url}]";
        }

        public ulong GetTaskKey()
        {
            var hashCode = CalculateStringHash(Database);
            hashCode = (hashCode * 397) ^ CalculateStringHash(Url);
            return hashCode;
        }
    }
}
