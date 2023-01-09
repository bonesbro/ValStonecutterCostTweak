using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BronzeStoneworking
{
	[BepInPlugin(PluginId, BronzeStonePlugin.ModName, BronzeStonePlugin.Version)]
	public class BronzeStonePlugin : BaseUnityPlugin
	{
		private const string PluginId = "bonesbro.val.bronzestone";
		public const string Version = "1.0.1";
		public const string ModName = "Bronze Stoneworking";
		Harmony _Harmony;
		public static ManualLogSource Log;
		protected static bool PatchingHasAlreadySucceeded = false;

		private void Awake()
		{
#if DEBUG
			Log = Logger;
#else
			Log = new ManualLogSource(null);
#endif
			_Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
			Config.Bind<int>("General", "NexusID", 938, "Nexus mod ID for updates");
		}

		private void OnDestroy()
		{
			_Harmony?.UnpatchAll(PluginId);
		}

		[HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
		public static class ObjectDB_CopyOtherDB_Patch
		{
			public static void Postfix()
			{
				BronzeStonePlugin.ModifyCosts("ObjectDB.CopyOtherDB");
			}
		}

		internal static void ModifyCosts(string debugInfo)
		{
			//Debug.Log("[bronzestone]: Running during " + debugInfo);

			//DumpRecipes();
			//DumpItems();
			//DumpPieceTables();
			//DumpColliders();

			// When we first wake up the ObjectDB hasn't been instantiated yet
			if (ObjectDB.instance == null)
			{
				Debug.LogError("[bronzestone]: ObjectDB is null");
				return;
			}

			GameObject hammer = ObjectDB.instance.GetItemPrefab("Hammer");
			if (hammer == null)
			{
				Debug.LogError("[bronzestone]: Could not find Hammer in ObjectDB");
				return;
			}

			ItemDrop hammerItemDrop;
			if (!hammer.TryGetComponent<ItemDrop>(out hammerItemDrop))
			{
				Debug.LogError("[bronzestone]: Could not get itemdrop from hammer");
				return;
			}

			PieceTable hammerPieceTable = hammerItemDrop?.m_itemData?.m_shared?.m_buildPieces;
			if (hammerPieceTable == null)
			{
				Debug.LogError("[bronzestone]: Could not find piecetable in hammer");
				return;
			}

			GameObject stonecutter = hammerPieceTable.m_pieces.Find(i => i.name == "piece_stonecutter");
			if (stonecutter == null)
			{
				Debug.LogError("[bronzestone]: Could not find piece_stonecutter in hammer's piecetable");
				return;
			}

			Piece piece;
			if (!stonecutter.TryGetComponent<Piece>(out piece))
			{
				Debug.LogError("[bronzestone]: Could not find Piece component in piece_stonecutter");
				return;
			}

			if (piece.m_resources == null)
			{
				Debug.LogError("[bronzestone]: piece_stonecutter.m_resources is null");
				return;
			}

			if (piece.m_resources.Length == 0)
			{
				Debug.LogError("[bronzestone]: piece_stonecutter.m_resources.Length == 0");
				return;
			}

			// Try to find the Iron item in the requirements
			bool foundResource = false;
			foreach (Piece.Requirement req in piece.m_resources)
			{
				if (req.m_resItem.name != "Iron")
					continue;

				foundResource = true;

				// Look up the Bronze resource
				GameObject bronzePrefab = ObjectDB.instance.GetItemPrefab("Bronze");
				if (bronzePrefab == null)
				{
					Debug.LogError("[bronzestone]: Did not find Bronze resource when trying to change requirements");
					return;
				}

				ItemDrop bronzeItem;
				if (!bronzePrefab.TryGetComponent<ItemDrop>(out bronzeItem))
				{
					Debug.LogError("[bronzestone]: Did not find Bronze component from its prefab");
					return;
				}

				// Replace the iron with bronze
				req.m_resItem = bronzeItem;
			}

			if (!foundResource)
			{
				// No need to log if we've already patched it
				if (!PatchingHasAlreadySucceeded)
				{
					Debug.Log("[bronzestone]: Did not find Iron resource when trying to change requirements.  This is expected when loading multiple worlds without restarting.");
				}

				return;
			}

			PatchingHasAlreadySucceeded = true;
			Debug.Log("[bronzestone]: Recipe updated successfully!");
		}

		private static void DumpRecipes()
		{
			// dump all recipes.  these look like just crafting bench recipes and not hammer recipes though
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				string resources = "";
				foreach (Piece.Requirement req in recipe.m_resources)
				{
					resources += $"{req.m_resItem.name}:{req.m_amount}, ";
				}
				Debug.Log($"[recipedump] {recipe.name}: {resources}");
			}
		}

		private static void DumpItems()
		{
			// dump all items.  these only seem like actual items in the game (or special effects) but don't include buildable Pieces
			foreach (GameObject gameobj in ObjectDB.instance.m_items)
			{
				DumpGameObject(gameobj);
			}
		}

		private static void DumpGameObject(GameObject gameobj)
		{
			string logmsg = $"[itemdump] {gameobj.name} tag:{gameobj.tag} layer:{gameobj.layer}";

			Piece piece;
			if (gameobj.TryGetComponent<Piece>(out piece))
			{
				logmsg += ", piece reqs: ";
				foreach (Piece.Requirement req in piece.m_resources)
				{
					logmsg += $"{req.m_resItem.name}:{req.m_amount}, ";
				}
			}

			Debug.Log(logmsg);

			if (gameobj.name == "wood_floor" || gameobj.name == "wood_roof")
			{
				Transform tr = gameobj.GetComponent<Transform>();
				//Piece pi = gameobj.GetComponent<Piece>();
				ZNetView zn = gameobj.GetComponent<ZNetView>();
				WearNTear wn = gameobj.GetComponent<WearNTear>();

				Debug.Log($"Transform: name:{tr.name ?? "[null]"} tag:{tr.tag ?? "[null]"}, children: {tr.childCount}");
				//Debug.Log($"Piece: name:{pi.name ?? "[null]"} tag:{pi.tag ?? "[null]"}");
				//Debug.Log($"ZNetView: name:{zn.name ?? "[null]"} tag:{zn.tag ?? "[null]"}");
				//Debug.Log($"WearNTear: name:{wn.name ?? "[null]"} tag:{wn.tag ?? "[null]"} {wn.m_noRoofWear} {wn.m_noSupportWear}");

				for (int iChild=0; iChild < tr.childCount; iChild++)
				{
					Transform trChild = tr.GetChild(iChild);
					Debug.Log($"   child transform {iChild}: name:{trChild.name ?? "[null]"} tag:{trChild.tag ?? "[null]"}, children: {trChild.childCount}");
				}

			}

		}

		private static void DumpCollider(Collider col)
		{
			if (col == null)
				return;

			Debug.Log($"    col: {col.name ?? "[null]"} tag:{col.tag ?? "[null]"}");
		}

		private static void DumpPieceTables()
		{
			// find all items that can build things
			foreach (GameObject gameobj in ObjectDB.instance.m_items)
			{
				ItemDrop drop = gameobj.GetComponent<ItemDrop>();

				// and find the list of everything they can build
				PieceTable table = drop?.m_itemData?.m_shared?.m_buildPieces;
				if (table == null)
					continue;

				string logmsg = $"[piecedump] {gameobj.name} {drop.name} {table.name} {table.m_pieces.Count}";
				string itemsInTable = table.m_pieces.Join(n => n.name, ",");

				Debug.Log(logmsg + " => " + itemsInTable);

				foreach (GameObject buildable in table.m_pieces)
				{
					DumpGameObject(buildable);
				}
			}
		}

		private static void DumpColliders()
		{
			Debug.Log("--- dumping global colliders ---");
			foreach (Collider col in Piece.pieceColliders)
			{
				DumpCollider(col);
			}
		}
	}
}
