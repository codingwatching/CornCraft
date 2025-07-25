﻿namespace CraftSharp.Protocol.Handlers
{
    /// <summary>
    /// Incoming packet types
    /// </summary>
    public enum PacketTypesIn
    {
        AcknowledgePlayerDigging,   //
        ActionBar,                  //
        Advancements,               //
        AttachEntity,               //
        Bundle,                     // Added in 1.19.4
        BlockAction,                //
        BlockBreakAnimation,        //
        BlockChange,                //
        BlockChangedAck,            // Added in 1.19
        BlockEntityData,            //
        BossBar,                    //
        Camera,                     //
        ChangeGameState,            //
        ChatMessage,                //
        ChatPreview,                // Added in 1.19
        ChatSuggestions,            // Added in 1.19.1 (1.19.2)
        ChunkBatchFinished,         // Added in 1.20.2
        ChunkBatchStarted,          // Added in 1.12.2
        ChunksBiomes,               // Added in 1.19.4
        ChunkData,                  //
        ClearTiles,                 //
        CloseInventory,             //
        CollectItem,                //
        CombatEvent,                //
        CookieRequest,              // Added in 1.20.6
        CraftRecipeResponse,        //
        CustomReportDetails,        // Added in 1.21 (Not used)
        DamageEvent,                // Added in 1.19.4
        DeathCombatEvent,           //
        DebugSample,                // Added in 1.20.6
        DeclareCommands,            //
        DeclareRecipes,             //
        DestroyEntities,            //
        Disconnect,                 //
        DisplayScoreboard,          //
        Effect,                     //
        EndCombatEvent,             //
        EnterCombatEvent,           //
        EntityAnimation,            //
        EntityEffect,               //
        EntityEquipment,            //
        EntityHeadLook,             //
        EntityMetadata,             //
        EntityMovement,             //
        EntityPosition,             //
        EntityPositionAndRotation,  //
        EntityProperties,           //
        EntityRotation,             //
        EntitySoundEffect,          //
        EntityStatus,               //
        EntityTeleport,             //
        EntityVelocity,             //
        Explosion,                  //
        FacePlayer,                 //
        FeatureFlags,               // Added in 1.19.3
        HeldItemChange,             //
        HideMessage,                // Added in 1.19.1 (1.19.2)
        HurtAnimation,              // Added in 1.19.4
        InitializeWorldBorder,      //
        JoinGame,                   //
        KeepAlive,                  //
        MapChunkBulk,               // For 1.8 or below
        MapData,                    //
        MessageHeader,              // Added in 1.19.1 (1.19.2)
        MultiBlockChange,           //
        NamedSoundEffect,           //
        NBTQueryResponse,           //
        OpenBook,                   //
        OpenHorseWindow,            //
        OpenSignEditor,             //
        OpenInventory,              //
        Particle,                   //
        Ping,                       //
        PingResponse,               // Added in 1.20.2
        PlayerAbilities,            //
        PlayerInfo,                 //
        PlayerListHeaderAndFooter,  //
        PlayerRemove,               // Added in 1.19.3 (Not used)
        PlayerPositionAndLook,      //
        PluginMessage,              //
        ProfilelessChatMessage,     // Added in 1.19.3
        ProjectilePower,            // Added in 1.20.6
        RemoveEntityEffect,         //
        RemoveResourcePack,         // Added in 1.20.3
        ResetScore,                 // Added in 1.20.3
        ResourcePackSend,           //
        Respawn,                    //
        ScoreboardObjective,        //
        SelectAdvancementTab,       //
        ServerData,                 // Added in 1.19
        ServerDifficulty,           //
        ServerLinks,                // Added in 1.21 (Not used)
        SetCompression,             // For 1.8 or below
        SetCooldown,                //
        SetDisplayChatPreview,      // Added in 1.19
        SetExperience,              //
        SetPassengers,              //
        SetSlot,                    //
        SetTickingState,            // Added in 1.20.3
        StepTick,                   // Added in 1.20.3
        SetTitleSubTitle,           //
        SetTitleText,               //
        SetTitleTime,               //
        SkulkVibrationSignal,       //
        SoundEffect,                //
        SpawnEntity,                //
        SpawnExperienceOrb,         //
        SpawnLivingEntity,          //
        SpawnPainting,              //
        SpawnPlayer,                //
        SpawnPosition,              //
        SpawnWeatherEntity,         //
        StartConfiguration,         // Added in 1.20.2
        Statistics,                 //
        StopSound,                  //
        StoreCookie,                // Added in 1.20.6
        SystemChat,                 // Added in 1.19
        TabComplete,                //
        Tags,                       //
        Teams,                      //
        TimeUpdate,                 //
        Title,                      //
        TradeList,                  //
        Transfer,                   // Added in 1.20.6
        Unknown,                    // For old version packet that have been removed and not used by mcc 
        UnloadChunk,                //
        UnlockRecipes,              //
        UpdateEntityNBT,            // For 1.8 or below
        UpdateHealth,               //
        UpdateLight,                //
        UpdateScore,                //
        UpdateSign,                 // For 1.8 or below
        UpdateSimulationDistance,   //
        UpdateViewDistance,         //
        UpdateViewPosition,         //
        UseBed,                     // For 1.13.2 or below
        VehicleMove,                //
        InventoryConfirmation,      //
        InventoryItems,             //
        InventoryProperty,          //
        WorldBorder,                //
        WorldBorderCenter,          //
        WorldBorderLerpSize,        //
        WorldBorderSize,            //
        WorldBorderWarningDelay,    //
        WorldBorderWarningReach,    //
    }
}
