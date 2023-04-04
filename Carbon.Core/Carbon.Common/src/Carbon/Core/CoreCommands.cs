﻿/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

using Carbon.Extensions;
using System;
using Carbon.Plugins;
using API.Hooks;
using Carbon.Base.Interfaces;
using Carbon.Components;
using Newtonsoft.Json;
using Oxide.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Facepunch;
using System.IO;

namespace Carbon.Core;
#pragma warning disable IDE0051

public partial class CorePlugin : CarbonPlugin
{

	#region App

	// DISABLED UNTIL FULLY FUNCTIONAL
	// [ConsoleCommand("exit", "Completely unloads Carbon from the game, rendering it fully vanilla.")]
	// private void Exit(ConsoleSystem.Arg arg)
	// {
	// 	//FIXMENOW
	// 	//Supervisor.ASM.UnloadModule("Carbon.dll", false);
	// }

	// DISABLED UNTIL FULLY FUNCTIONAL
	// [ConsoleCommand("reboot", "Unloads Carbon from the game and then loads it back again with the latest version changes (if any).")]
	// private void Reboot(ConsoleSystem.Arg arg)
	// {
	// 	//FIXMENOW
	// 	//Supervisor.ASM.UnloadModule("Carbon.dll", true);
	// }

	[ConsoleCommand("version", "Returns currently loaded version of Carbon.")]
	private void GetVersion(ConsoleSystem.Arg arg)
	{
		Reply($"Carbon v{Community.Runtime.Analytics.Version}", arg);
	}

	[ConsoleCommand("build", "Returns current version of Carbon's Assembly.")]
	private void GetBuild(ConsoleSystem.Arg arg)
	{
		Reply($"{Community.Runtime.Analytics.InformationalVersion}", arg);
	}

	[ConsoleCommand("plugins", "Prints the list of mods and their loaded plugins.")]
	private void Plugins(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var mode = arg.HasArgs(1) ? arg.Args[0] : null;

		switch (mode)
		{
			case "-j":
			case "--j":
			case "-json":
			case "--json":
				Reply(JsonConvert.SerializeObject(Loader.LoadedMods, Formatting.Indented), arg);
				break;

			default:
				var body = new StringTable("#", "Mod", "Author", "Version", "Hook Time", "Compile Time");
				var count = 1;

				foreach (var mod in Loader.LoadedMods)
				{
					if (mod.IsCoreMod) continue;

					body.AddRow($"{count:n0}", $"{mod.Name}{(mod.Plugins.Count > 1 ? $" ({mod.Plugins.Count:n0})" : "")}", "", "", "", "");

					foreach (var plugin in mod.Plugins)
					{
						body.AddRow($"", plugin.Name, plugin.Author, $"v{plugin.Version}", $"{plugin.TotalHookTime:0.0}s", $"{plugin.CompileTime:0}ms");
					}

					count++;
				}

				Reply(body.ToStringMinimal(), arg);
				break;
		}
	}

	[ConsoleCommand("pluginsfailed", "Prints the list of plugins that failed to load (most likely due to compilation issues).")]
	private void PluginsFailed(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		var mode = arg.HasArgs(1) ? arg.Args[0] : null;

		switch (mode)
		{
			case "-j":
			case "--j":
			case "-json":
			case "--json":
				Reply(JsonConvert.SerializeObject(Loader.FailedMods, Formatting.Indented), arg);
				break;

			default:
				var result = string.Empty;
				var count = 1;

				foreach (var mod in Loader.FailedMods)
				{
					result += $"{count:n0}. {mod.File}\n";

					foreach (var error in mod.Errors)
					{
						result += $" {error}\n";
					}

					result += "\n";
					count++;
				}

				Reply(result, arg);
				break;
		}
	}

	// DISABLED UNTIL FULLY FUNCTIONAL
	// [ConsoleCommand("update", "Downloads, updates, saves the server and patches Carbon at runtime. (Eg. c.update win develop, c.update unix prod)")]
	// private void Update(ConsoleSystem.Arg arg)
	// {
	// 	if (!arg.IsPlayerCalledAndAdmin()) return;

	// 	Updater.DoUpdate((bool result) =>
	// 	{
	// 		if (!result)
	// 		{
	// 			Logger.Error($"Unknown error while updating Carbon");
	// 			return;
	// 		}
	// 		HookCaller.CallStaticHook("OnServerSave");

	// 		//FIXMENOW
	// 		//Supervisor.ASM.UnloadModule("Carbon.dll", true);
	// 	});
	// }

	#endregion

#if DEBUG
	[ConsoleCommand("assembly", "Debug stuff.")]
	private void AssemblyInfo(ConsoleSystem.Arg arg)
	{
		if (!arg.IsPlayerCalledAndAdmin()) return;

		int count = 0;
		StringTable body = new StringTable("#", "Assembly", "Version", "Dynamic", "Location");
		foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			body.AddRow($"{count++:n0}", assembly.GetName().Name, assembly.GetName().Version, assembly.IsDynamic, (assembly.IsDynamic) ? string.Empty : assembly.Location);
		Reply(body.ToStringMinimal(), arg);
	}
#endif

	#region Conditionals

	[ConsoleCommand("addconditional", "Adds a new conditional compilation symbol to the compiler.")]
	[AuthLevel(2)]
	private void AddConditional(ConsoleSystem.Arg arg)
	{
		var value = arg.Args[0];

		if (!Community.Runtime.Config.ConditionalCompilationSymbols.Contains(value))
		{
			Community.Runtime.Config.ConditionalCompilationSymbols.Add(value);
			Community.Runtime.SaveConfig();
			Reply($"Added conditional '{value}'.", arg);
		}
		else
		{
			Reply($"Conditional '{value}' already exists.", arg);
		}

		foreach (var mod in Loader.LoadedMods)
		{
			var plugins = Pool.GetList<RustPlugin>();
			plugins.AddRange(mod.Plugins);

			foreach (var plugin in plugins)
			{
				if (plugin.HasConditionals)
				{
					plugin._processor_instance.Dispose();
					plugin._processor_instance.Execute();
					mod.Plugins.Remove(plugin);
				}
			}

			Pool.FreeList(ref plugins);
		}
	}

	[ConsoleCommand("remconditional", "Removes an existent conditional compilation symbol from the compiler.")]
	[AuthLevel(2)]
	private void RemoveConditional(ConsoleSystem.Arg arg)
	{
		var value = arg.Args[0];

		if (Community.Runtime.Config.ConditionalCompilationSymbols.Contains(value))
		{
			Community.Runtime.Config.ConditionalCompilationSymbols.Remove(value);
			Community.Runtime.SaveConfig();
			Reply($"Removed conditional '{value}'.", arg);
		}
		else
		{
			Reply($"Conditional '{value}' does not exist.", arg);
		}

		foreach (var mod in Loader.LoadedMods)
		{
			var plugins = Pool.GetList<RustPlugin>();
			plugins.AddRange(mod.Plugins);

			foreach (var plugin in plugins)
			{
				if (plugin.HasConditionals)
				{
					plugin._processor_instance.Dispose();
					plugin._processor_instance.Execute();
					mod.Plugins.Remove(plugin);
				}
			}

			Pool.FreeList(ref plugins);
		}
	}

	[ConsoleCommand("conditionals", "Prints a list of all conditional compilation symbols used by the compiler.")]
	[AuthLevel(2)]
	private void Conditionals(ConsoleSystem.Arg arg)
	{
		Reply($"Conditionals ({Community.Runtime.Config.ConditionalCompilationSymbols.Count:n0}): {Community.Runtime.Config.ConditionalCompilationSymbols.ToArray().ToString(", ", " and ")}", arg);
	}

	#endregion

	#region Hooks

	[ConsoleCommand("hooks", "Prints the list of all hooks that have been called at least once.")]
	[AuthLevel(2)]
	private void HookInfo(ConsoleSystem.Arg arg)
	{
		var body = new StringTable("#", "Name", "Hook", "Id", "Type", "Status", "Total", "Sub");
		int count = 0, success = 0, warning = 0, failure = 0;

		var option1 = arg.GetString(0, null);
		var option2 = arg.GetString(1, null);

		switch (option1)
		{
			case "loaded":
				{
					IEnumerable<IHook> hooks;

					switch (option2)
					{
						case "--patch":
							hooks = Community.Runtime.HookManager.LoadedPatches.Where(x => !x.IsHidden);
							break;

						case "--static":
							hooks = Community.Runtime.HookManager.LoadedStaticHooks.Where(x => !x.IsHidden);
							break;

						case "--dynamic":
							hooks = Community.Runtime.HookManager.LoadedDynamicHooks.Where(x => !x.IsHidden);
							break;

						default:
							hooks = Community.Runtime.HookManager.LoadedPatches.Where(x => !x.IsHidden);
							hooks = hooks.Concat(Community.Runtime.HookManager.LoadedStaticHooks.Where(x => !x.IsHidden));
							hooks = hooks.Concat(Community.Runtime.HookManager.LoadedDynamicHooks.Where(x => !x.IsHidden));
							break;
					}

					foreach (var mod in hooks.OrderBy(x => x.HookFullName))
					{
						if (mod.Status == HookState.Failure) failure++;
						if (mod.Status == HookState.Success) success++;
						if (mod.Status == HookState.Warning) warning++;

						body.AddRow(
							$"{count++:n0}",
							mod.HookFullName,
							mod.HookName,
							mod.Identifier.Substring(mod.Identifier.Length - 6),
							mod.IsStaticHook ? "Static" : mod.IsPatch ? "Patch" : "Dynamic",
							mod.Status,
							//$"{HookCaller.GetHookTime(mod.HookName)}ms",
							$"{HookCaller.GetHookTotalTime(mod.HookName)}ms",
							(mod.IsStaticHook) ? "N/A" : $"{Community.Runtime.HookManager.GetHookSubscriberCount(mod.Identifier),3}"
						);
					}

					Reply($"total:{count} success:{success} warning:{warning} failed:{failure}"
						+ Environment.NewLine + Environment.NewLine + body.ToStringMinimal(), arg);
					break;
				}

			default: // list installed
				{
					IEnumerable<IHook> hooks;

					switch (option1)
					{
						case "--patch":
							hooks = Community.Runtime.HookManager.InstalledPatches.Where(x => !x.IsHidden);
							break;

						case "--static":
							hooks = Community.Runtime.HookManager.InstalledStaticHooks.Where(x => !x.IsHidden);
							break;

						case "--dynamic":
							hooks = Community.Runtime.HookManager.InstalledDynamicHooks.Where(x => !x.IsHidden);
							break;

						default:
							hooks = Community.Runtime.HookManager.InstalledPatches.Where(x => !x.IsHidden);
							hooks = hooks.Concat(Community.Runtime.HookManager.InstalledStaticHooks.Where(x => !x.IsHidden));
							hooks = hooks.Concat(Community.Runtime.HookManager.InstalledDynamicHooks.Where(x => !x.IsHidden));
							break;
					}

					foreach (var mod in hooks.OrderBy(x => x.HookFullName))
					{
						if (mod.Status == HookState.Failure) failure++;
						if (mod.Status == HookState.Success) success++;
						if (mod.Status == HookState.Warning) warning++;

						body.AddRow(
							$"{count++:n0}",
							mod.HookFullName,
							mod.HookName,
							mod.Identifier.Substring(mod.Identifier.Length - 6),
							mod.IsStaticHook ? "Static" : mod.IsPatch ? "Patch" : "Dynamic",
							mod.Status,
							//$"{HookCaller.GetHookTime(mod.HookName)}ms",
							$"{HookCaller.GetHookTotalTime(mod.HookName)}ms",
							(mod.IsStaticHook) ? "N/A" : $"{Community.Runtime.HookManager.GetHookSubscriberCount(mod.Identifier),3}"
						);
					}

					Reply($"total:{count} success:{success} warning:{warning} failed:{failure}"
						+ Environment.NewLine + Environment.NewLine + body.ToStringMinimal(), arg);
					break;
				}
		}
	}

	#endregion

	#region Config

	[ConsoleCommand("loadconfig", "Loads Carbon config from file.")]
	[AuthLevel(2)]
	private void CarbonLoadConfig(ConsoleSystem.Arg arg)
	{
		if (Community.Runtime == null) return;

		Community.Runtime.LoadConfig();

		Reply("Loaded Carbon config.", arg);
	}

	[ConsoleCommand("saveconfig", "Saves Carbon config to file.")]
	[AuthLevel(2)]
	private void CarbonSaveConfig(ConsoleSystem.Arg arg)
	{
		if (Community.Runtime == null) return;

		Community.Runtime.SaveConfig();

		Reply("Saved Carbon config.", arg);
	}

	[CommandVar("autoupdate", "Updates carbon hooks on boot.")]
	[AuthLevel(2)]
	private bool AutoUpdate { get { return Community.Runtime.Config.AutoUpdate; } set { Community.Runtime.Config.AutoUpdate = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("modding", "Mark this server as modded or not.")]
	[AuthLevel(2)]
	private bool Modding { get { return Community.Runtime.Config.IsModded; } set { Community.Runtime.Config.IsModded = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("higherpriorityhookwarns", "Print warns if hooks with higher priority conflict with other hooks. Best to keep this disabled. Same-priority hooks will be printed.")]
	[AuthLevel(2)]
	private bool HigherPriorityHookWarns { get { return Community.Runtime.Config.HigherPriorityHookWarns; } set { Community.Runtime.Config.HigherPriorityHookWarns = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("harmonyreference", "Reference 0Harmony.dll into plugins. Highly not recommended as plugins that patch methods might create a lot of instability to Carbon's core.")]
	[AuthLevel(2)]
	private bool HarmonyReference { get { return Community.Runtime.Config.HarmonyReference; } set { Community.Runtime.Config.HarmonyReference = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("debug", "The level of debug logging for Carbon. Helpful for very detailed logs in case things break. (Set it to -1 to disable debug logging.)")]
	[AuthLevel(2)]
	private int CarbonDebug { get { return Community.Runtime.Config.LogVerbosity; } set { Community.Runtime.Config.LogVerbosity = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("logfiletype", "The mode for writing the log to file. (0=disabled, 1=saves updates every 5 seconds, 2=saves immediately)")]
	[AuthLevel(2)]
	private int LogFileType { get { return Community.Runtime.Config.LogFileMode; } set { Community.Runtime.Config.LogFileMode = Mathf.Clamp(value, 0, 2); Community.Runtime.SaveConfig(); } }

	[CommandVar("unitystacktrace", "Enables a big chunk of detail of Unity's default stacktrace. Recommended to be disabled as a lot of it is internal and unnecessary for the average user.")]
	[AuthLevel(2)]
	private bool UnityStacktrace
	{
		get { return Community.Runtime.Config.UnityStacktrace; }
		set
		{
			Community.Runtime.Config.UnityStacktrace = value;
			Community.Runtime.SaveConfig();
			ApplyStacktrace();
		}
	}

	[CommandVar("hooktimetracker", "For debugging purposes, this will track the time of hooks and gives a total.")]
	[AuthLevel(2)]
	private bool HookTimeTracker { get { return Community.Runtime.Config.HookTimeTracker; } set { Community.Runtime.Config.HookTimeTracker = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("hookvalidation", "Prints a warning when plugins contain Oxide hooks that aren't available yet in Carbon.")]
	[AuthLevel(2)]
	private bool HookValidation { get { return Community.Runtime.Config.HookValidation; } set { Community.Runtime.Config.HookValidation = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("filenamecheck", "It checks if the file name and the plugin name matches. (only applies to scripts)")]
	[AuthLevel(2)]
	private bool FileNameCheck { get { return Community.Runtime.Config.FileNameCheck; } set { Community.Runtime.Config.FileNameCheck = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("entitymapbuffersize", "The entity map buffer size. Gets applied on Carbon reboot.")]
	[AuthLevel(2)]
	private int EntityMapBufferSize { get { return Community.Runtime.Config.EntityMapBufferSize; } set { Community.Runtime.Config.EntityMapBufferSize = value; Community.Runtime.SaveConfig(); } }

	[CommandVar("language", "Server language used by the Language API.")]
	[AuthLevel(2)]
	private string Language { get { return Community.Runtime.Config.Language; } set { Community.Runtime.Config.Language = value; Community.Runtime.SaveConfig(); } }

#if WIN
	[CommandVar("consoleinfo", "Show the Windows-only Carbon information at the bottom of the console.")]
	[AuthLevel(2)]
	private bool ConsoleInfo
	{
		get { return Community.Runtime.Config.ShowConsoleInfo; }
		set
		{
			Community.Runtime.Config.ShowConsoleInfo = value;

			if (value)
			{
				Community.Runtime.RefreshConsoleInfo();
			}
			else
			{
				if (ServerConsole.Instance != null && ServerConsole.Instance.input != null)
				{
					ServerConsole.Instance.input.statusText = new string[3];
				}
			}
		}
	}
#endif

	#endregion

	#region Commands

	[ConsoleCommand("find", "Searches through Carbon-processed console commands.")]
	[AuthLevel(2)]
	private void Find(ConsoleSystem.Arg arg)
	{
		var body = new StringTable("Command", "Value", "Help");
		var filter = arg.Args != null && arg.Args.Length > 0 ? arg.Args[0] : null;

		foreach (var command in Community.Runtime.AllConsoleCommands)
		{
			if (command.IsHidden || (!string.IsNullOrEmpty(filter) && !command.Command.Contains(filter))) continue;

			var value = " ";

			if (command.Reference != null)
			{
				if (command.Reference is FieldInfo field) value = field.GetValue(command.Plugin)?.ToString();
				else if (command.Reference is PropertyInfo property) value = property.GetValue(command.Plugin)?.ToString();
			}

			if (command.Protected)
			{
				value = new string('*', value.Length);
			}

			body.AddRow(command.Command, value, command.Help);
		}

		Reply($"Console Commands:\n{body.ToStringMinimal()}", arg);
	}

	[ConsoleCommand("findchat", "Searches through Carbon-processed chat commands.")]
	[AuthLevel(2)]
	private void FindChat(ConsoleSystem.Arg arg)
	{
		var body = new StringTable("Command", "Help");
		var filter = arg.Args != null && arg.Args.Length > 0 ? arg.Args[0] : null;

		foreach (var command in Community.Runtime.AllChatCommands)
		{
			if (command.IsHidden || (!string.IsNullOrEmpty(filter) && !command.Command.Contains(filter))) continue;

			body.AddRow(command.Command, command.Help);
		}

		Reply($"Chat Commands:\n{body.ToStringMinimal()}", arg);
	}

	#endregion

	#region Report

	[ConsoleCommand("report", "Reloads all current plugins, and returns a report based on them at the output path.")]
	[AuthLevel(2)]
	private void Report(ConsoleSystem.Arg arg)
	{
		new Carbon.Components.Report().Init();
	}

	#endregion

	#region Modules

	[ConsoleCommand("setmodule", "Enables or disables Carbon modules. Visit root/carbon/modules and use the config file names as IDs.")]
	[AuthLevel(2)]
	private void SetModule(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(2)) return;

		var hookable = Community.Runtime.ModuleProcessor.Modules.FirstOrDefault(x => x.Name == arg.Args[0]);
		var module = hookable?.To<IModule>();

		if (module == null)
		{
			Reply($"Couldn't find that module. Try 'c.modules' to print them all.", arg);
			return;
		}

		var previousEnabled = module.GetEnabled();
		var newEnabled = arg.Args[1].ToBool();

		if (previousEnabled != newEnabled)
		{
			module.SetEnabled(newEnabled);
			module.Save();
		}

		Reply($"{module.Name} marked {(module.GetEnabled() ? "enabled" : "disabled")}.", arg);
	}

	[ConsoleCommand("saveallmodules", "Saves the configs and data files of all available modules.")]
	[AuthLevel(2)]
	private void SaveAllModules(ConsoleSystem.Arg arg)
	{
		foreach (var hookable in Community.Runtime.ModuleProcessor.Modules)
		{
			var module = hookable.To<IModule>();
			module.Save();
		}

		Reply($"Saved {Community.Runtime.ModuleProcessor.Modules.Count:n0} module configs and data files.", arg);
	}

	[ConsoleCommand("savemoduleconfig", "Saves Carbon module config & data file.")]
	[AuthLevel(2)]
	private void SaveModuleConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(1)) return;

		var hookable = Community.Runtime.ModuleProcessor.Modules.FirstOrDefault(x => x.Name == arg.Args[0]);
		var module = hookable.To<IModule>();

		if (module == null)
		{
			Reply($"Couldn't find that module.", arg);
			return;
		}

		module.Save();

		Reply($"Saved '{module.Name}' module config & data file.", arg);
	}

	[ConsoleCommand("loadmoduleconfig", "Loads Carbon module config & data file.")]
	[AuthLevel(2)]
	private void LoadModuleConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(1)) return;

		var hookable = Community.Runtime.ModuleProcessor.Modules.FirstOrDefault(x => x.Name == arg.Args[0]);
		var module = hookable.To<IModule>();

		if (module == null)
		{
			Reply($"Couldn't find that module.", arg);
			return;
		}

		if (module.GetEnabled()) module.SetEnabled(false);
		module.Load();
		if (module.GetEnabled()) module.OnEnableStatus();

		Reply($"Reloaded '{module.Name}' module config.", arg);
	}

	[ConsoleCommand("modules", "Prints a list of all available modules.")]
	[AuthLevel(2)]
	private void Modules(ConsoleSystem.Arg arg)
	{
		using var print = new StringTable("Name", "Is Enabled", "Quick Command");
		foreach (var hookable in Community.Runtime.ModuleProcessor.Modules)
		{
			if (hookable is not IModule module) continue;

			print.AddRow(hookable.Name, module.GetEnabled() ? "Yes" : "No", $"c.setmodule \"{hookable.Name}\" 0/1");
		}

		Reply(print.ToStringMinimal(), arg);
	}

	#endregion

	#region Mod & Plugin Loading

	[ConsoleCommand("reload", "Reloads all or specific mods / plugins. E.g 'c.reload *' to reload everything.")]
	[AuthLevel(2)]
	private void Reload(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(1)) return;

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				Community.Runtime.ReloadPlugins();
				break;

			default:
				var path = GetPluginPath(name);

				if (!string.IsNullOrEmpty(path))
				{
					Community.Runtime.ScriptProcessor.ClearIgnore(path);
					Community.Runtime.ScriptProcessor.Prepare(name, path);
					return;
				}

				var pluginFound = false;

				foreach (var mod in Loader.LoadedMods)
				{
					var plugins = Pool.GetList<RustPlugin>();
					plugins.AddRange(mod.Plugins);

					foreach (var plugin in plugins)
					{
						if (plugin.Name == name)
						{
							plugin._processor_instance.Dispose();
							plugin._processor_instance.Execute();
							mod.Plugins.Remove(plugin);
							pluginFound = true;
						}
					}

					Pool.FreeList(ref plugins);
				}

				if (!pluginFound)
				{
					Logger.Warn($"Plugin {name} was not found or was typed incorrectly.");
				}
				break;
		}
	}

	[ConsoleCommand("load", "Loads all mods and/or plugins. E.g 'c.load *' to load everything you've unloaded.")]
	[AuthLevel(2)]
	private void LoadPlugin(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(1))
		{
			Logger.Warn("You must provide the name of a plugin or use * to load all plugins.");
			return;
		}

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				//
				// Scripts
				//
				{
					var tempList = Pool.GetList<string>();
					tempList.AddRange(Community.Runtime.ScriptProcessor.IgnoreList);
					Community.Runtime.ScriptProcessor.IgnoreList.Clear();

					foreach (var plugin in tempList)
					{
						Community.Runtime.ScriptProcessor.Prepare(Path.GetFileNameWithoutExtension(plugin), plugin);
					}

					Pool.FreeList(ref tempList);
					break;
				}

			default:
				{
					var path = GetPluginPath(name);
					if (!string.IsNullOrEmpty(path))
					{
						Community.Runtime.ScriptProcessor.ClearIgnore(path);
						Community.Runtime.ScriptProcessor.Prepare(path);
						return;
					}

					Logger.Warn($"Plugin {name} was not found or was typed incorrectly.");

					/*var module = BaseModule.GetModule<DRMModule>();
					foreach (var drm in module.Config.DRMs)
					{
						foreach (var entry in drm.Entries)
						{
							if (entry.Id == name) drm.RequestEntry(entry);
						}
					}*/
					break;
				}
		}
	}

	[ConsoleCommand("unload", "Unloads all mods and/or plugins. E.g 'c.unload *' to unload everything. They'll be marked as 'ignored'.")]
	[AuthLevel(2)]
	private void UnloadPlugin(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(1))
		{
			Logger.Warn("You must provide the name of a plugin or use * to unload all plugins.");
			return;
		}

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				//
				// Scripts
				//
				{
					var tempList = Pool.GetList<string>();

					foreach (var bufferInstance in Community.Runtime.ScriptProcessor.InstanceBuffer)
					{
						tempList.Add(bufferInstance.Value.File);
					}

					Community.Runtime.ScriptProcessor.IgnoreList.Clear();
					Community.Runtime.ScriptProcessor.Clear();

					foreach (var plugin in tempList)
					{
						Community.Runtime.ScriptProcessor.Ignore(plugin);
					}
				}

				//
				// Web-Scripts
				//
				{
					var tempList = Pool.GetList<string>();
					tempList.AddRange(Community.Runtime.WebScriptProcessor.IgnoreList);
					Community.Runtime.WebScriptProcessor.IgnoreList.Clear();
					Community.Runtime.WebScriptProcessor.Clear();

					foreach (var plugin in tempList)
					{
						Community.Runtime.WebScriptProcessor.Ignore(plugin);
					}
					Pool.FreeList(ref tempList);
					break;
				}

			default:
				{
					var path = GetPluginPath(name);
					if (!string.IsNullOrEmpty(path))
					{
						Community.Runtime.ScriptProcessor.Ignore(path);
						Community.Runtime.WebScriptProcessor.Ignore(path);
					}

					var pluginFound = false;

					foreach (var mod in Loader.LoadedMods)
					{
						var plugins = Pool.GetList<RustPlugin>();
						plugins.AddRange(mod.Plugins);

						foreach (var plugin in plugins)
						{
							if (plugin.Name == name)
							{
								plugin._processor_instance.Dispose();
								mod.Plugins.Remove(plugin);
								pluginFound = true;
							}
						}

						Pool.FreeList(ref plugins);
					}

					if (!pluginFound)
					{
						Logger.Warn($"Plugin {name} was not found or was typed incorrectly.");
					}
					break;
				}
		}
	}

	[ConsoleCommand("reloadconfig", "Reloads a plugin's config file. This might have unexpected results, use cautiously.")]
	[AuthLevel(2)]
	private void ReloadConfig(ConsoleSystem.Arg arg)
	{
		if (!arg.HasArgs(1))
		{
			Logger.Warn("You must provide the name of a plugin or use * to reload all plugin configs.");
			return;
		}

		RefreshOrderedFiles();

		var name = arg.Args[0];
		switch (name)
		{
			case "*":
				{

					foreach (var package in Loader.LoadedMods)
					{
						foreach (var plugin in package.Plugins)
						{
							plugin.ILoadConfig();
							plugin.Load();
							plugin.Puts($"Reloaded plugin's config.");
						}
					}

					break;
				}

			default:
				{
					var pluginFound = false;

					foreach (var mod in Loader.LoadedMods)
					{
						var plugins = Pool.GetList<RustPlugin>();
						plugins.AddRange(mod.Plugins);

						foreach (var plugin in plugins)
						{
							if (plugin.Name == name)
							{
								plugin.ILoadConfig();
								plugin.Load();
								plugin.Puts($"Reloaded plugin's config.");
								pluginFound = true;
							}
						}

						Pool.FreeList(ref plugins);
					}

					if (!pluginFound)
					{
						Logger.Warn($"Plugin {name} was not found or was typed incorrectly.");
					}
					break;
				}
		}
	}

	#endregion

	#region Permissions

	[ConsoleCommand("grant", "Grant one or more permissions to users or groups. Do 'c.grant' for syntax info.")]
	[AuthLevel(2)]
	private void Grant(ConsoleSystem.Arg arg)
	{
		void PrintWarn()
		{
			Reply($"Syntax: c.grant <user|group> <name|id> <perm>", arg);
		}

		if (!arg.HasArgs(3))
		{
			PrintWarn();
			return;
		}

		var action = arg.Args[0];
		var name = arg.Args[1];
		var perm = arg.Args[2];
		var user = permission.FindUser(name);

		switch (action)
		{
			case "user":
				if (permission.GrantUserPermission(user.Key, perm, null))
				{
					Reply($"Granted user '{user.Value.LastSeenNickname}' permission '{perm}'", arg);
				}
				break;

			case "group":
				if (permission.GrantGroupPermission(name, perm, null))
				{
					Reply($"Granted group '{name}' permission '{perm}'", arg);
				}
				break;

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("revoke", "Revoke one or more permissions from users or groups. Do 'c.revoke' for syntax info.")]
	[AuthLevel(2)]
	private void Revoke(ConsoleSystem.Arg arg)
	{
		void PrintWarn()
		{
			Reply($"Syntax: c.revoke <user|group> <name|id> <perm>", arg);
		}

		if (!arg.HasArgs(3))
		{
			PrintWarn();
			return;
		}

		var action = arg.Args[0];
		var name = arg.Args[1];
		var perm = arg.Args[2];
		var user = permission.FindUser(name);

		switch (action)
		{
			case "user":
				if (permission.RevokeUserPermission(user.Key, perm))
				{
					Reply($"Revoked user '{user.Value?.LastSeenNickname}' permission '{perm}'", arg);
				}
				break;

			case "group":
				if (permission.RevokeGroupPermission(name, perm))
				{
					Reply($"Revoked group '{name}' permission '{perm}'", arg);
				}
				break;

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("show", "Displays information about a specific player or group (incl. permissions, groups and user list). Do 'c.show' for syntax info.")]
	[AuthLevel(2)]
	private void Show(ConsoleSystem.Arg arg)
	{
		void PrintWarn()
		{
			Reply($"Syntax: c.show <groups|perms>", arg);
			Reply($"Syntax: c.show <group|user> <name|id>", arg);
		}

		if (!arg.HasArgs(1)) { PrintWarn(); return; }

		var action = arg.Args[0];

		switch (action)
		{
			case "user":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var name = arg.Args[1];
					var user = permission.FindUser(name);

					if (user.Value == null)
					{
						Reply($"Couldn't find that user.", arg);
						return;
					}

					Reply($"User {user.Value.LastSeenNickname}[{user.Key}] found in {user.Value.Groups.Count:n0} groups:\n  {user.Value.Groups.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					Reply($"and has {user.Value.Perms.Count:n0} permissions:\n  {user.Value.Perms.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					break;
				}
			case "group":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var name = arg.Args[1];

					if (!permission.GroupExists(name))
					{
						Reply($"Couldn't find that group.", arg);
						return;
					}

					var users = permission.GetUsersInGroup(name);
					var permissions = permission.GetGroupPermissions(name, false);
					Reply($"Group {name} has {users.Length:n0} users:\n  {users.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					Reply($"and has {permissions.Length:n0} permissions:\n  {permissions.Select(x => x).ToArray().ToString(", ", " and ")}", arg);
					break;
				}
			case "groups":
				{
					var groups = permission.GetGroups();
					if (groups.Count() == 0)
					{
						Reply($"Couldn't find any group.", arg);
						return;
					}

					Reply($"Groups:\n {String.Join(", ", groups)}", arg);
					break;
				}
			case "perms":
				{
					var perms = permission.GetPermissions();
					if (perms.Count() == 0)
					{
						Reply($"Couldn't find any permission.", arg);
					}

					Reply($"Permissions:\n {String.Join(", ", perms)}", arg);

					break;
				}

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("usergroup", "Adds or removes a player from a group. Do 'c.usergroup' for syntax info.")]
	[AuthLevel(2)]
	private void UserGroup(ConsoleSystem.Arg arg)
	{
		void PrintWarn()
		{
			Reply($"Syntax: c.usergroup <add|remove> <player> <group>", arg);
		}

		if (!arg.HasArgs(3))
		{
			PrintWarn();
			return;
		}

		var action = arg.Args[0];
		var player = arg.Args[1];
		var group = arg.Args[2];

		var user = permission.FindUser(player);

		if (user.Value == null)
		{
			Reply($"Couldn't find that player.", arg);
			return;
		}

		if (!permission.GroupExists(group))
		{
			Reply($"Group '{group}' could not be found.", arg);
			return;
		}

		switch (action)
		{
			case "add":
				if (permission.UserHasGroup(user.Key, group))
				{
					Reply($"{user.Value.LastSeenNickname}[{user.Key}] is already in '{group}' group.", arg);
					return;
				}

				permission.AddUserGroup(user.Key, group);
				Reply($"Added {user.Value.LastSeenNickname}[{user.Key}] to '{group}' group.", arg);
				break;

			case "remove":
				if (!permission.UserHasGroup(user.Key, group))
				{
					Reply($"{user.Value.LastSeenNickname}[{user.Key}] isn't in '{group}' group.", arg);
					return;
				}

				permission.RemoveUserGroup(user.Key, group);
				Reply($"Removed {user.Value.LastSeenNickname}[{user.Key}] from '{group}' group.", arg);
				break;

			default:
				PrintWarn();
				break;
		}
	}

	[ConsoleCommand("group", "Adds or removes a group. Do 'c.group' for syntax info.")]
	[AuthLevel(2)]
	private void Group(ConsoleSystem.Arg arg)
	{
		void PrintWarn()
		{
			Reply($"Syntax: c.group add <group> [<displayName>] [<rank>]", arg);
			Reply($"Syntax: c.group remove <group>", arg);
			Reply($"Syntax: c.group set <group> <title|rank> <value>", arg);
			Reply($"Syntax: c.group parent <group> [<parent>]", arg);
		}

		if (!arg.HasArgs(1)) { PrintWarn(); return; }

		var action = arg.Args[0];

		switch (action)
		{
			case "add":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var group = arg.Args[1];

					if (permission.GroupExists(group))
					{
						Reply($"Group '{group}' already exists. To set any values for this group, use 'c.group set'.", arg);
						return;
					}

					if (permission.CreateGroup(group, arg.HasArgs(3) ? arg.Args[2] : group, arg.HasArgs(4) ? arg.Args[3].ToInt() : 0))
					{
						Reply($"Created '{group}' group.", arg);
					}
				}
				break;

			case "set":
				{
					if (!arg.HasArgs(4)) { PrintWarn(); return; }

					var group = arg.Args[1];

					if (!permission.GroupExists(group))
					{
						Reply($"Group '{group}' does not exists.", arg);
						return;
					}

					var set = arg.Args[2];
					var value = arg.Args[3];

					switch (set)
					{
						case "title":
							permission.SetGroupTitle(group, value);
							break;

						case "rank":
							permission.SetGroupRank(group, value.ToInt());
							break;
					}

					Reply($"Set '{group}' group.", arg);
				}
				break;

			case "remove":
				{
					if (!arg.HasArgs(2)) { PrintWarn(); return; }

					var group = arg.Args[1];

					if (permission.RemoveGroup(group)) Reply($"Removed '{group}' group.", arg);
					else Reply($"Couldn't remove '{group}' group.", arg);
				}
				break;

			case "parent":
				{
					if (!arg.HasArgs(3)) { PrintWarn(); return; }

					var group = arg.Args[1];
					var parent = arg.Args[2];

					if (permission.SetGroupParent(group, parent)) Reply($"Changed '{group}' group's parent to '{parent}'.", arg);
					else Reply($"Couldn't change '{group}' group's parent to '{parent}'.", arg);
				}
				break;

			default:
				PrintWarn();
				break;
		}
	}

	#endregion
}