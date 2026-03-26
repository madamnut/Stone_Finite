


using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public static partial class WorldDataGenerator
    {

        private const ushort ID_AIR = 0;
        private const ushort ID_ROCK = 1;
        private const ushort ID_DIRT = 2;

        private const ushort ID_GRASS_TOP = 3;
        private const ushort ID_GRASS_LEFT = 4;
        private const ushort ID_GRASS_RIGHT = 5;
        private const ushort ID_GRASS_TOPLEFT = 6;
        private const ushort ID_GRASS_TOPRIGHT = 7;
        private const ushort ID_GRASS_LEFTRIGHT = 8;
        private const ushort ID_GRASS_TOPLEFTRIGHT = 9;

        private const ushort ID_CLAY = 10;
        private const ushort ID_MUD = 11;

        private const ushort ID_SAND = 1000;
        private const ushort ID_GRAVEL = 1001;

        private const ushort ID_TRUNK = 2000;
        private const ushort ID_LEAF = 2001;
        private const ushort ID_PLANT = 2002;
        private const ushort ID_BUSH = 2003;
        private const ushort ID_STONE_PILE = 2004;
        private const ushort ID_SMALL_STONE_PILE = 2005;
        private const ushort ID_DEAD_BUSH = 2006;
        private const ushort ID_AGAVE_0 = 2007;
        private const ushort ID_AGAVE_1 = 2008;
        private const ushort ID_AGAVE_2 = 2009;
        private const ushort ID_AGAVE_3 = 2010;
        private const ushort ID_AGAVE_4 = 2011;
        private const ushort ID_AGAVE_5 = 2012;
        private const ushort ID_CACTUS = 2013;
        private const ushort ID_SNOW = 2014;
        private const ushort ID_FROZEN_BUSH = 2015;
        private const ushort ID_FROZEN_PLANT = 2016;
        private const ushort ID_FROZEN_TRUNK = 2017;
        private const ushort ID_FLAX_TOP = 2020;
        private const ushort ID_FLAX_BOTTOM = 2021;

        private const ushort ID_ORE_COAL = 3000;
        private const ushort ID_ORE_COPPER = 3001;
        private const ushort ID_ORE_IRON = 3002;
        private const ushort ID_ORE_TIN = 3003;

        private const ushort ID_GRANITE = 4000;
        private const ushort ID_AMPHIBOLITE = 4001;

        private const ushort ID_SANDSTONE = 35;
        private const ushort ID_SANDSTONE_BRICK = 36;

        private const ushort ID_FROZEN_DIRT = 46;
        private const ushort ID_FROZEN_GRASS_TOP = 37;
        private const ushort ID_FROZEN_GRASS_LEFT = 38;
        private const ushort ID_FROZEN_GRASS_RIGHT = 39;
        private const ushort ID_FROZEN_GRASS_TOPLEFT = 40;
        private const ushort ID_FROZEN_GRASS_TOPRIGHT = 41;
        private const ushort ID_FROZEN_GRASS_LEFTRIGHT = 42;
        private const ushort ID_FROZEN_GRASS_TOPLEFTRIGHT = 43;
        private const ushort ID_ICE_CELL = 44;
        private const ushort ID_SNOW_CELL = 45;

        private const ushort ID_BASALT = 47;
        private const ushort ID_TUFF = 48;
        private const ushort ID_ANDESITE = 49;

        private const ushort FLUID_NONE = 0;
        private const ushort FLUID_WATER = 1;
        private const ushort FLUID_LAVA = 2;

        private const byte NATURAL_MAX = 15;

        private const int SALT_DESERT_START = unchecked((int)0x0D35E12);
        private const int SALT_DESERT_PASS = unchecked((int)0x0D35E12A);
        private const int SALT_SAND_BFS = unchecked((int)0x0A11CE);
        private const int SALT_DECOR = unchecked((int)0x00DEC0);
        private const int SALT_SNOW_END = unchecked((int)0x0510001);
        private const int SALT_SNOW_PASS = unchecked((int)0x0510005A);
        private const int SALT_MAGMA = unchecked((int)0x0BADC0DE);

        
        private static void StepLog(string label, float stepStart, float totalStart)
        {
            float now = Time.realtimeSinceStartup;
            Debug.Log($"[WorldGen] {label}: {(now - stepStart) * 1000f:F1} ms (total {(now - totalStart) * 1000f:F1} ms)");
        }
    }
}
