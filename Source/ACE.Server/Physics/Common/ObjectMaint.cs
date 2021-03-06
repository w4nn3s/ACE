using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Physics.Common
{
    public class ObjectMaint
    {
        public bool IsActive;
        public Dictionary<uint, LostCell> LostCellTable;
        public Dictionary<uint, PhysicsObj> ObjectTable;
        public List<PhysicsObj> NullObjectTable;
        public Dictionary<uint, WeenieObject> WeenieObjectTable;
        public List<WeenieObject> NullWeenieObjectTable;
        public Dictionary<uint, PhysicsObj> VisibleObjectTable;
        public Dictionary<PhysicsObj, double> DestructionObjectTable;
        public Dictionary<uint, int> ObjectInventoryTable;
        public Queue<double> ObjectDestructionQueue;

        /// <summary>
        /// Objects are removed from the client after this amount of time
        /// </summary>
        public static readonly float DestructionTime = 25.0f;

        public ObjectMaint()
        {
            LostCellTable = new Dictionary<uint, LostCell>();
            ObjectTable = new Dictionary<uint, PhysicsObj>();
            NullObjectTable = new List<PhysicsObj>();
            WeenieObjectTable = new Dictionary<uint, WeenieObject>();
            NullWeenieObjectTable = new List<WeenieObject>();
            VisibleObjectTable = new Dictionary<uint, PhysicsObj>();
            DestructionObjectTable = new Dictionary<PhysicsObj, double>();
            ObjectInventoryTable = new Dictionary<uint, int>();
            ObjectDestructionQueue = new Queue<double>();
        }

        /// <summary>
        /// Adds an object to the list of known objects
        /// </summary>
        /// <returns>true if previously an unknown object</returns>
        public bool AddObject(PhysicsObj obj)
        {
            if (!ObjectTable.ContainsKey(obj.ID))
            {
                ObjectTable.Add(obj.ID, obj);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds a list of objects to the known objects list
        /// </summary>
        /// <param name="objs">A list of newly visible objects</param>
        /// <returns>The list of visible objects that were previously unknown</returns>
        public List<PhysicsObj> AddObjects(List<PhysicsObj> objs)
        {
            var newObjs = new List<PhysicsObj>();

            foreach (var obj in objs)
            {
                if (AddObject(obj)) newObjs.Add(obj);

                RemoveObjectToBeDestroyed(obj);
            }
            return newObjs;
        }

        public bool AddObjectToBeDestroyed(PhysicsObj obj)
        {
            var time = Timer.CurrentTime + DestructionTime;
            if (!DestructionObjectTable.ContainsKey(obj))
            {
                //Console.WriteLine("Adding object to be destroyed " + obj.ID + " at " + time);
                DestructionObjectTable.Add(obj, time);
                ObjectDestructionQueue.Enqueue(time);
                return true;
            }
            //else
                //DestructionObjectTable[objectID] = time;
            return false;
        }

        public List<PhysicsObj> AddObjectsToBeDestroyed(List<PhysicsObj> objs)
        {
            var queued = new List<PhysicsObj>();
            foreach (var obj in objs)
            {
                if (AddObjectToBeDestroyed(obj))
                    queued.Add(obj);
            }
            return queued;
        }

        public bool AddVisibleObject(PhysicsObj obj)
        {
            if (!VisibleObjectTable.ContainsKey(obj.ID))
            {
                VisibleObjectTable.Add(obj.ID, obj);
                return true;
            }
            return false;
        }

        public List<PhysicsObj> AddVisibleObjects(List<PhysicsObj> objs)
        {
            foreach (var obj in objs) AddVisibleObject(obj);

            return AddObjects(objs);
        }

        public void AddWeenieObject(WeenieObject wobj)
        {
            if (!WeenieObjectTable.ContainsKey(wobj.ID))
                WeenieObjectTable.Add(wobj.ID, wobj);
            else
                WeenieObjectTable[wobj.ID] = wobj;
        }

        public List<PhysicsObj> GetCulledObjects(List<PhysicsObj> visibleObjects)
        {
            var culledObjects = DestructionObjectTable.Where(kvp => kvp.Value > Timer.CurrentTime).ToDictionary(kvp => kvp.Key, kvp => kvp.Value).Keys.ToList();
            return culledObjects;
        }

        public List<PhysicsObj> GetDestroyedObjects()
        {
            var destroyedObjects = DestructionObjectTable.Where(kvp => kvp.Value <= Timer.CurrentTime).ToDictionary(kvp => kvp.Key, kvp => kvp.Value).Keys.ToList();
            return destroyedObjects;
        }

        public LostCell GetLostCell(uint cellID)
        {
            LostCell lostCell = null;
            LostCellTable.TryGetValue(cellID, out lostCell);
            return lostCell;
        }

        public PhysicsObj GetObjectA(uint objectID)
        {
            PhysicsObj obj = null;
            ObjectTable.TryGetValue(objectID, out obj);
            return obj;
        }

        public bool GetObjectA(uint objectID, ref PhysicsObj obj, ref WeenieObject wobj)
        {
            obj = GetObjectA(objectID);
            wobj = GetWeenieObject(objectID);
            return (obj != null || wobj != null);
        }

        public int GetObjectInventory(uint objectID)
        {
            var inventory = 0;
            ObjectInventoryTable.TryGetValue(objectID, out inventory);
            return inventory;
        }

        public List<PhysicsObj> GetVisibleObjects(EnvCell cell)
        {
            var visibleObjs = new List<PhysicsObj>();

            foreach (var envCell in cell.VisibleCells.Values)
            {
                if (envCell == null) continue; 
                visibleObjs.AddRange(envCell.ObjectList);
            }

            return visibleObjs.Distinct().ToList();
        }

        public WeenieObject GetWeenieObject(uint objectID)
        {
            WeenieObject wobj = null;
            WeenieObjectTable.TryGetValue(objectID, out wobj);
            return wobj;
        }

        public void GotoLostCell(PhysicsObj obj, uint cellID)
        {
            if (obj.Parent != null) return;
            obj.set_cell_id(cellID);
            var lostCell = GetLostCell(obj.Position.ObjCellID);
            if (lostCell == null) return;
            lostCell.Objects.Add(obj);
            lostCell.NumObjects++;
        }

        public void InitObjCell(ObjCell cell)
        {
            var lostCell = GetLostCell(cell.ID);
            if (lostCell == null) return;
            foreach (var obj in lostCell.Objects)
                obj.reenter_visibility();
            lostCell.Clear();   // remove from list?
        }

        public void ReleaseObjCell(ObjCell objCell)
        {
            var removeList = new List<PhysicsObj>();

            foreach (var obj in objCell.ObjectList)
            {
                if (!obj.State.HasFlag(PhysicsState.Static) && obj.Parent == null)
                    removeList.Add(obj);
            }
            foreach (var obj in removeList)
            {
                objCell.ObjectList.Remove(obj);
                obj.leave_visibility();
            }
            objCell.NumObjects = objCell.ObjectList.Count;
        }

        public void RemoveFromLostCell(PhysicsObj obj)
        {
            if (obj.CurCell != null || obj.Parent != null) return;
            var lostCell = GetLostCell(obj.Position.ObjCellID);
            if (lostCell != null)
                lostCell.remove_object(obj);
        }

        public bool RemoveObjectToBeDestroyed(PhysicsObj obj)
        {
            double time = -1;
            DestructionObjectTable.TryGetValue(obj, out time);
            if (time != -1 && time > Timer.CurrentTime)
            {
                DestructionObjectTable.Remove(obj);
                return true;
            }
            return false;
        }

        public void RemoveObjectsToBeDestroyed(List<PhysicsObj> objs)
        {
            foreach (var obj in objs)
                RemoveObjectToBeDestroyed(obj);
        }
    }
}
