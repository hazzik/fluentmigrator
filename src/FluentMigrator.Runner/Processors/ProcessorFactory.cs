using System;
using System.Collections.Generic;
using System.Linq;

namespace FluentMigrator.Runner.Processors
{
	public static class ProcessorFactory
	{
		public static IMigrationProcessorFactory GetFactory(string processorName)
		{
			foreach (var factory in Factories)
			{
				var type = factory.GetType();
				var name = type.Name;
				if (name.StartsWith(processorName, StringComparison.OrdinalIgnoreCase))
				{
					return factory;
				}
			}

			return null;
		}

		public static string ListAvailableProcessorTypes()
		{
			var strings = GetProcessorTypes()
				.OrderBy(x => x.Name)
				.Select(processorType => processorType.Name)
				.Select(name => name.Substring(0, name.IndexOf("ProcessorFactory")))
				.ToArray();

			return string.Join(", ", strings).ToLowerInvariant();
		}

		private static readonly object factoriesLock = new object();

		private volatile static IEnumerable<IMigrationProcessorFactory> factories;

		public static IEnumerable<IMigrationProcessorFactory> Factories
		{
			get
			{
				if (factories == null)
				{
					lock (factoriesLock)
					{
						if (factories == null)
						{
							factories = GetProcessorTypes()
								.Select(Activator.CreateInstance)
								.OfType<IMigrationProcessorFactory>()
								.ToList();
						}
					}

				}

				return factories;
			}
		}

		private static IEnumerable<Type> GetProcessorTypes()
		{
			return typeof(IMigrationProcessorFactory).Assembly.GetExportedTypes()
				.Where(t => typeof(IMigrationProcessorFactory).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
		}

		public static IMigrationProcessorFactory GetFactoryForProvider(string providerName)
		{
			return Factories.Where(f => f.IsForProvider(providerName)).FirstOrDefault();
		}
	}
}