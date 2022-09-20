﻿using Carbon.Core;
using Harmony;

namespace Carbon.Extended
{
    [CarbonHook ( "OnEntitySpawn" ), CarbonHook.Category ( CarbonHook.Category.Enum.Entity )]
    [CarbonHook.Parameter ( "entity", typeof ( BaseNetworkable ) )]
    [CarbonHook.Info ( "Called before any networked entity has spawned (including trees)." )]
    [CarbonHook.Patch ( typeof ( BaseNetworkable ), "Spawn" )]
    public class BaseNetworkable_Spawn_OnEntitySpawn
    {
        public static void Prefix ( ref BaseNetworkable __instance )
        {
            HookExecutor.CallStaticHook ( "OnEntitySpawn", __instance );
        }
    }
}