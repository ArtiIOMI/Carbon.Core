﻿using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using API.Hooks;
using HarmonyLib;
using UnityEngine;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Hooks;

public partial class Category_Fixes
{
	public partial class Fixes_ItemCrafter
	{
		[HookAttribute.Patch("IRecyclerThinkSpeed", typeof(Recycler), "StartRecycling", new System.Type[] { })]
		[HookAttribute.Identifier("a07a3f25546d49d2b140d6cbf6453aa0")]
		[HookAttribute.Options(HookFlags.Hidden)]

		public class Fixes_ItemCrafter_GetScaledDuration_a07a3f25546d49d2b140d6cbf6453aa0
		{
			private static bool Prefix(Recycler __instance)
			{
				var hook = HookCaller.CallStaticHook("IRecyclerThinkSpeed", __instance);

				if (hook is float value)
				{
					if (__instance.IsOn())
					{
						return false;
					}

					__instance.InvokeRepeating(__instance.RecycleThink, value, value);
					Effect.server.Run(__instance.startSound.resourcePath, __instance, 0U, Vector3.zero, Vector3.zero, null, false);
					__instance.SetFlag(BaseEntity.Flags.On, true, false, true);
					__instance.SendNetworkUpdateImmediate(false);
					return false;
				}

				return true;
			}
		}
	}
}
