﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Acklann.Plaid;
using DryIoc;
using EnumsNET;
using ImTools;
using Making.Cents.Data;
using Making.Cents.Services;
using Making.Cents.ViewModels;
using Making.Cents.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Making.Cents
{
	internal class Bootstrapper
	{

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool AllocConsole();

		public static void Run()
		{
			var container = new Container();
			container.RegisterInstance(BuildConfiguration());

			InitializeLogging(container);

			var logger = container.Resolve<ILogger<Bootstrapper>>();
			logger.LogDebug("Logging initialized");

			RegisterDataSources(container);
			RegisterServices(container);
			RegisterViewModels(container);
			logger.LogDebug("DryIoC initialized");

			InitializeDatabase(container);
			logger.LogDebug("Database initialized");

			var vm = container.Resolve<ShellViewModel>();

			var window =
				new ShellView
				{
					DataContext = vm,
				};
			window.Show();

			_ = vm.InitializeAsync();
			logger.LogDebug("Application started");
		}

		private static void InitializeLogging(Container container)
		{
			AllocConsole();

			Log.Logger = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.MinimumLevel.Debug()
				.WriteTo.Console(
					outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj} <s:{SourceContext}>{NewLine}{Exception}",
					theme: AnsiConsoleTheme.Code)
				.CreateLogger();

			var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory();

			container.RegisterInstance<ILoggerFactory>(factory);
			container.Register(
				Made.Of(() => factory.CreateLogger(null)),
				setup: Setup.With(condition: r => r.Parent.ImplementationType == null));
			container.Register(
				Made.Of(
					() => factory.CreateLogger(Arg.Index<Type>(0)),
					r => r.Parent.ImplementationType),
				setup: Setup.With(condition: r => r.Parent.ImplementationType != null));

			var method = typeof(LoggerFactoryExtensions)
				.GetMethods(BindingFlags.Public | BindingFlags.Static)
				.Where(m => m.Name == nameof(LoggerFactoryExtensions.CreateLogger))
				.Where(m => m.ContainsGenericParameters)
				.Single();

			container.Register(
				typeof(ILogger<>),
				made: Made.Of(method));
		}

		private static IConfigurationRoot BuildConfiguration()
		{
			var builder = new ConfigurationBuilder()
				.AddJsonFile("appsettings.json", optional: false)
				.AddJsonFile("appsettings.secrets.json", optional: true);

			var configuration = builder.Build();
			return configuration;
		}

		private static void RegisterDataSources(Container container)
		{
			container.Register<DbContext>(Reuse.Transient, setup: Setup.With(allowDisposableTransient: true));

			var configuration = container.Resolve<IConfigurationRoot>().GetSection("Plaid");

			var environment = Enums.Parse<Acklann.Plaid.Environment>(configuration["environment"]);
			var clientId = configuration["clientId"];
			var secret = configuration["secret"];

			foreach (var (key, value) in configuration.GetSection("accessTokens").AsEnumerable())
			{
				container.RegisterInstance(
					new PlaidClient(
						environment: environment,
						clientId: clientId,
						secret: secret,
						accessToken: value),
					serviceKey: key);
			}
		}

		private static void RegisterServices(Container container)
		{
			container.Register<AccountService>(Reuse.Singleton);
		}

		private static void RegisterViewModels(Container container)
		{
			container.Register<ShellViewModel>(Reuse.Singleton);
		}

		private static void InitializeDatabase(Container container)
		{
			using (var context = container.Resolve<DbContext>())
				context.InitializeDatabase();
		}
	}
}