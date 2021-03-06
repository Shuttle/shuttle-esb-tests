using System;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Tests
{
    public class MemoryQueueFactory : IQueueFactory
    {
        public string Scheme => MemoryQueue.Scheme;

        public IQueue Create(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            return new MemoryQueue(uri);
        }

        public bool CanCreate(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            return Scheme.Equals(uri.Scheme, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}