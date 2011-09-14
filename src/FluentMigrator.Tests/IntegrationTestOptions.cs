namespace FluentMigrator.Tests
{
	public static class IntegrationTestOptions
	{
		public static readonly DatabaseServerOptions SqlServer2005
			= new DatabaseServerOptions
			  	{
			  		ConnectionString = @"server=.\SQLEXPRESS;uid=;pwd=;Trusted_Connection=yes;database=FluentMigrator",
			  		IsEnabled = false
			  	};

		public static readonly DatabaseServerOptions SqlServer2008
			= new DatabaseServerOptions
			  	{
			  		ConnectionString = @"server=.\SQLEXPRESS;uid=;pwd=;Trusted_Connection=yes;database=FluentMigrator",
			  		IsEnabled = false
			  	};

		public static readonly DatabaseServerOptions SqlLite
			= new DatabaseServerOptions
			  	{
			  		ConnectionString = @"Data Source=:memory:;Version=3;New=True;",
			  		IsEnabled = false
			  	};

		public static readonly DatabaseServerOptions MySql
			= new DatabaseServerOptions
			  	{
			  		ConnectionString = @"Database=FluentMigrator;Data Source=localhost;User Id=test;Password=test;Allow User Variables=True",
			  		IsEnabled = false
			  	};

		public static readonly DatabaseServerOptions Postgres
			= new DatabaseServerOptions
			  	{
			  		ConnectionString = "Server=127.0.0.1;Port=5432;Database=FluentMigrator;User Id=test;Password=test;",
			  		IsEnabled = false
			  	};

		public class DatabaseServerOptions
		{
			public string ConnectionString;
			public bool IsEnabled = true;
		}
	}
}