﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using Shuttle.Core.Infrastructure;

namespace Shuttle.Esb.Tests
{
	public class ResourceUsageFixture : IntegrationFixture
	{
		public void TestResourceUsage(string queueUriFormat, bool isTransactional)
		{
			const int threadCount = 5;

			var configuration = GetConfiguration(queueUriFormat, isTransactional, threadCount);

            var container = GetComponentContainer(configuration);

            var transportMessageFactory = container.Resolve<ITransportMessageFactory>();
            var serializer = container.Resolve<ISerializer>();
            var events = container.Resolve<IServiceBusEvents>();

            var cpuCounter = new PerformanceCounterValue(new PerformanceCounter
			{
				CategoryName = "Processor",
				CounterName = "% Processor Time",
				InstanceName = "_Total"
			});

			var padlock = new object();
			var idleThreads = new List<int>();
			var startDate = DateTime.Now;
			var endDate = startDate.AddSeconds(10);
			var iteration = 0;
			float cpuUsageLimit;
			float cpuMaximumUsage = 0f;

			cpuCounter.NextValue();
			Thread.Sleep(1000);
			cpuUsageLimit = cpuCounter.NextValue() + 25F;

			using (var bus = ServiceBus.Create(container).Start())
			{
				events.ThreadWaiting += (sender, args) =>
				{
					lock (padlock)
					{
						if (idleThreads.Contains(Thread.CurrentThread.ManagedThreadId))
						{
							return;
						}

						idleThreads.Add(Thread.CurrentThread.ManagedThreadId);
					}
				};

				while (DateTime.Now < endDate)
				{
					iteration++;

					for (var i = 0; i < 5; i++)
					{
						var message = transportMessageFactory.Create(new SimpleCommand("[resource testing]"),
							c => c.WithRecipient(configuration.Inbox.WorkQueue));

						configuration.Inbox.WorkQueue.Enqueue(message, serializer.Serialize(message));
					}

					idleThreads.Clear();

					Console.WriteLine("[checking usage] : iteration = {0}", iteration);

					while (idleThreads.Count < threadCount)
					{
						var cpuUsage = cpuCounter.NextValue();

						if (cpuUsage > cpuMaximumUsage)
						{
							cpuMaximumUsage = cpuUsage;
						}

						Assert.IsTrue(cpuUsage < cpuUsageLimit,
							string.Format("[EXCEEDED] : cpu usage = {0} / limit = {1}", cpuUsage, cpuUsageLimit));

						Thread.Sleep(25);
					}
				}

				Console.WriteLine("[done] : started = '{0}' / end = '{1}'", startDate, endDate);
				Console.WriteLine("[CPU] : maximum usage = {0} / cpu usage limit = {1}", cpuMaximumUsage, cpuUsageLimit);
			}
		}

		private static ServiceBusConfiguration GetConfiguration(string queueUriFormat, bool isTransactional, int threadCount)
		{
		    using (var queueManager = GetQueueManager())
		    {
		        var configuration = DefaultConfiguration(isTransactional);

		        var inboxWorkQueue = queueManager.GetQueue(string.Format(queueUriFormat, "test-inbox-work"));
		        var errorQueue = queueManager.GetQueue(string.Format(queueUriFormat, "test-error"));

		        configuration.Inbox =
		            new InboxQueueConfiguration
		            {
		                WorkQueue = inboxWorkQueue,
		                ErrorQueue = errorQueue,
		                DurationToSleepWhenIdle = new[] {TimeSpan.FromSeconds(1)},
		                ThreadCount = threadCount
		            };

		        inboxWorkQueue.AttemptDrop();
		        errorQueue.AttemptDrop();

		        queueManager.CreatePhysicalQueues(configuration);

		        inboxWorkQueue.AttemptPurge();
		        errorQueue.AttemptPurge();

		        return configuration;
		    }
		}
	}
}