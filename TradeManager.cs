if (OtherInventory == null || MyInventory == null)
                 InitializeTrade (me, other);
 
-            var t = new Trade (me, other, sessionId, token, apiKey, MyInventory, OtherInventory);
+            var t = new Trade (me, other, sessionId, token, MyInventory, OtherInventory);
 
             t.OnClose += delegate
             {
@@ -227,10 +227,10 @@ public void InitializeTrade (SteamID me, SteamID other)
             // fetch other player's inventory from the Steam API.
             OtherInventory = Inventory.FetchInventory (other.ConvertToUInt64 (), apiKey);
 
-            if (OtherInventory == null)
-            {
-                throw new InventoryFetchException (other);
-            }
+            //if (OtherInventory == null)
+            //{
+            //    throw new InventoryFetchException (other);
+            //}
             
             // fetch our inventory from the Steam API.
             MyInventory = Inventory.FetchInventory (me.ConvertToUInt64 (), apiKey);
