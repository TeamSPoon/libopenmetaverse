using System;
using System.Collections.Generic;
using System.Threading;
using ExtensionLoader;
using OpenMetaverse;
using OpenMetaverse.Rendering;
using OpenMetaverse.Packets;

namespace Simian.Extensions
{
    public class ObjectManager : IExtension<Simian>
    {
        Simian server;
        
        public ObjectManager()
        {
        }

        public void Start(Simian server)
        {
            this.server = server;

            server.UDP.RegisterPacketCallback(PacketType.ObjectAdd, new PacketCallback(ObjectAddHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectAttach, new PacketCallback(ObjectAttachHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectDuplicate, new PacketCallback(ObjectDuplicateHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectSelect, new PacketCallback(ObjectSelectHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectDeselect, new PacketCallback(ObjectDeselectHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectLink, new PacketCallback(ObjectLinkHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectDelink, new PacketCallback(ObjectDelinkHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectShape, new PacketCallback(ObjectShapeHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectFlagUpdate, new PacketCallback(ObjectFlagUpdateHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectExtraParams, new PacketCallback(ObjectExtraParamsHandler));
            server.UDP.RegisterPacketCallback(PacketType.ObjectImage, new PacketCallback(ObjectImageHandler));
            server.UDP.RegisterPacketCallback(PacketType.Undo, new PacketCallback(UndoHandler));
            server.UDP.RegisterPacketCallback(PacketType.Redo, new PacketCallback(RedoHandler));
            server.UDP.RegisterPacketCallback(PacketType.DeRezObject, new PacketCallback(DeRezObjectHandler));
            server.UDP.RegisterPacketCallback(PacketType.MultipleObjectUpdate, new PacketCallback(MultipleObjectUpdateHandler));
            server.UDP.RegisterPacketCallback(PacketType.RequestObjectPropertiesFamily, new PacketCallback(RequestObjectPropertiesFamilyHandler));
        }

        public void Stop()
        {
        }

        void ObjectAddHandler(Packet packet, Agent agent)
        {
            ObjectAddPacket add = (ObjectAddPacket)packet;

            Vector3 position = Vector3.Zero;
            Vector3 scale = add.ObjectData.Scale;
            PCode pcode = (PCode)add.ObjectData.PCode;
            PrimFlags flags = (PrimFlags)add.ObjectData.AddFlags;
            //bool bypassRaycast = (add.ObjectData.BypassRaycast == 1);
            bool rayEndIsIntersection = (add.ObjectData.RayEndIsIntersection == 1);

            #region Position Calculation

            if (rayEndIsIntersection)
            {
                // HACK: Blindly trust where the client tells us to place
                position = add.ObjectData.RayEnd;
            }
            else
            {
                if (add.ObjectData.RayTargetID != UUID.Zero)
                {
                    SimulationObject obj;
                    if (server.Scene.TryGetObject(add.ObjectData.RayTargetID, out obj))
                    {
                        // Test for a collision with the specified object
                        position = server.Physics.ObjectCollisionTest(add.ObjectData.RayStart, add.ObjectData.RayEnd, obj);
                    }
                }

                if (position == Vector3.Zero)
                {
                    // Test for a collision with the entire scene
                    position = FullSceneCollisionTest(add.ObjectData.RayStart, add.ObjectData.RayEnd);
                }
            }

            // Position lies on the face of another surface, either terrain of an object.
            // Back up along the ray so we are not colliding with the mesh.
            // HACK: This is really cheesy and should be done by a collision system
            Vector3 rayDir = Vector3.Normalize(add.ObjectData.RayEnd - add.ObjectData.RayStart);
            position -= rayDir * scale;

            #endregion Position Calculation

            #region Foliage Handling

            // Set all foliage to phantom
            if (pcode == PCode.Grass || pcode == PCode.Tree || pcode == PCode.NewTree)
            {
                flags |= PrimFlags.Phantom;

                if (pcode != PCode.Grass)
                {
                    // Resize based on the foliage type
                    Tree tree = (Tree)add.ObjectData.State;

                    switch (tree)
                    {
                        case Tree.Cypress1:
                        case Tree.Cypress2:
                            scale = new Vector3(4f, 4f, 10f);
                            break;
                        default:
                            scale = new Vector3(4f, 4f, 4f);
                            break;
                    }
                }
            }

            #endregion Foliage Handling

            // Create an object
            Primitive prim = new Primitive();

            // TODO: Security check
            prim.GroupID = add.AgentData.GroupID;
            prim.ID = UUID.Random();
            prim.MediaURL = String.Empty;
            prim.OwnerID = agent.ID;
            prim.Position = position;

            prim.PrimData.Material = (Material)add.ObjectData.Material;
            prim.PrimData.PathCurve = (PathCurve)add.ObjectData.PathCurve;
            prim.PrimData.ProfileCurve = (ProfileCurve)add.ObjectData.ProfileCurve;
            prim.PrimData.PathBegin = Primitive.UnpackBeginCut(add.ObjectData.PathBegin);
            prim.PrimData.PathEnd = Primitive.UnpackEndCut(add.ObjectData.PathEnd);
            prim.PrimData.PathScaleX = Primitive.UnpackPathScale(add.ObjectData.PathScaleX);
            prim.PrimData.PathScaleY = Primitive.UnpackPathScale(add.ObjectData.PathScaleY);
            prim.PrimData.PathShearX = Primitive.UnpackPathShear((sbyte)add.ObjectData.PathShearX);
            prim.PrimData.PathShearY = Primitive.UnpackPathShear((sbyte)add.ObjectData.PathShearY);
            prim.PrimData.PathTwist = Primitive.UnpackPathTwist(add.ObjectData.PathTwist);
            prim.PrimData.PathTwistBegin = Primitive.UnpackPathTwist(add.ObjectData.PathTwistBegin);
            prim.PrimData.PathRadiusOffset = Primitive.UnpackPathTwist(add.ObjectData.PathRadiusOffset);
            prim.PrimData.PathTaperX = Primitive.UnpackPathTaper(add.ObjectData.PathTaperX);
            prim.PrimData.PathTaperY = Primitive.UnpackPathTaper(add.ObjectData.PathTaperY);
            prim.PrimData.PathRevolutions = Primitive.UnpackPathRevolutions(add.ObjectData.PathRevolutions);
            prim.PrimData.PathSkew = Primitive.UnpackPathTwist(add.ObjectData.PathSkew);
            prim.PrimData.ProfileBegin = Primitive.UnpackBeginCut(add.ObjectData.ProfileBegin);
            prim.PrimData.ProfileEnd = Primitive.UnpackEndCut(add.ObjectData.ProfileEnd);
            prim.PrimData.ProfileHollow = Primitive.UnpackProfileHollow(add.ObjectData.ProfileHollow);
            prim.PrimData.PCode = pcode;

            prim.Properties = new Primitive.ObjectProperties();
            prim.Properties.CreationDate = DateTime.Now;
            prim.Properties.CreatorID = agent.ID;
            prim.Properties.Description = String.Empty;
            prim.Properties.GroupID = add.AgentData.GroupID;
            prim.Properties.LastOwnerID = agent.ID;
            prim.Properties.Name = "New Object";
            prim.Properties.ObjectID = prim.ID;
            prim.Properties.OwnerID = prim.OwnerID;
            prim.Properties.Permissions = server.Permissions.GetDefaultPermissions();
            prim.Properties.SalePrice = 10;

            prim.RegionHandle = server.Scene.RegionHandle;
            prim.Rotation = add.ObjectData.Rotation;
            prim.Scale = scale;
            prim.TextColor = Color4.Black;

            // Add this prim to the object database
            SimulationObject simObj = new SimulationObject(prim, server);
            server.Scene.ObjectAddOrUpdate(this, simObj, agent.ID, 0, flags);
        }

        void ObjectAttachHandler(Packet packet, Agent agent)
        {
            ObjectAttachPacket attach = (ObjectAttachPacket)packet;

            for (int i = 0; i < attach.ObjectData.Length; i++)
            {
                SimulationObject obj;
                if (!server.Scene.TryGetObject(attach.ObjectData[i].ObjectLocalID, out obj))
                    continue;

                obj.Prim.ParentID = agent.Avatar.Prim.LocalID;
                obj.Prim.Position = Vector3.Zero; //TODO: simulationObject.AttachmentPoint
                obj.Prim.Rotation = attach.ObjectData[i].Rotation;  //TODO: simulationObject.AttachmentRot ?

                AttachmentPoint point = (AttachmentPoint)attach.AgentData.AttachmentPoint;
                obj.Prim.PrimData.AttachmentPoint = point == AttachmentPoint.Default ? obj.LastAttachmentPoint : point;

                // Send an update out to everyone
                server.Scene.ObjectAddOrUpdate(this, obj, agent.ID, 0, obj.Prim.Flags);
            }
        }

        void ObjectDuplicateHandler(Packet packet, Agent agent)
        {
            ObjectDuplicatePacket duplicate = (ObjectDuplicatePacket)packet;

            PrimFlags flags = (PrimFlags)duplicate.SharedData.DuplicateFlags;
            Vector3 offset = duplicate.SharedData.Offset;

            for (int i = 0; i < duplicate.ObjectData.Length; i++)
            {
                uint dupeID = duplicate.ObjectData[i].ObjectLocalID;

                SimulationObject obj;
                if (server.Scene.TryGetObject(dupeID, out obj))
                {
                    SimulationObject newObj = new SimulationObject(obj);
                    newObj.Prim.Position += offset;
                    newObj.Prim.ID = UUID.Zero;
                    newObj.Prim.LocalID = 0;
                    newObj.Prim.Properties.CreationDate = DateTime.Now;

                    server.Scene.ObjectAddOrUpdate(this, newObj, agent.ID, 0, flags);
                }
                else
                {
                    Logger.Log("ObjectDuplicate sent for missing object " + dupeID,
                        Helpers.LogLevel.Warning);

                    KillObjectPacket kill = new KillObjectPacket();
                    kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                    kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                    kill.ObjectData[0].ID = dupeID;
                    server.UDP.SendPacket(agent.ID, kill, PacketCategory.State);
                }
            }
        }

        void ObjectSelectHandler(Packet packet, Agent agent)
        {
            ObjectSelectPacket select = (ObjectSelectPacket)packet;

            for (int i = 0; i < select.ObjectData.Length; i++)
            {
                ObjectPropertiesPacket properties = new ObjectPropertiesPacket();
                properties.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
                properties.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();

                SimulationObject obj;
                if (server.Scene.TryGetObject(select.ObjectData[i].ObjectLocalID, out obj))
                {
                    //Logger.DebugLog("Selecting object " + obj.Prim.LocalID);

                    properties.ObjectData[0].BaseMask = (uint)obj.Prim.Properties.Permissions.BaseMask;
                    properties.ObjectData[0].CreationDate = Utils.DateTimeToUnixTime(obj.Prim.Properties.CreationDate);
                    properties.ObjectData[0].CreatorID = obj.Prim.Properties.CreatorID;
                    properties.ObjectData[0].Description = Utils.StringToBytes(obj.Prim.Properties.Description);
                    properties.ObjectData[0].EveryoneMask = (uint)obj.Prim.Properties.Permissions.EveryoneMask;
                    properties.ObjectData[0].GroupID = obj.Prim.Properties.GroupID;
                    properties.ObjectData[0].GroupMask = (uint)obj.Prim.Properties.Permissions.GroupMask;
                    properties.ObjectData[0].LastOwnerID = obj.Prim.Properties.LastOwnerID;
                    properties.ObjectData[0].Name = Utils.StringToBytes(obj.Prim.Properties.Name);
                    properties.ObjectData[0].NextOwnerMask = (uint)obj.Prim.Properties.Permissions.NextOwnerMask;
                    properties.ObjectData[0].ObjectID = obj.Prim.ID;
                    properties.ObjectData[0].OwnerID = obj.Prim.Properties.OwnerID;
                    properties.ObjectData[0].OwnerMask = (uint)obj.Prim.Properties.Permissions.OwnerMask;
                    properties.ObjectData[0].OwnershipCost = obj.Prim.Properties.OwnershipCost;
                    properties.ObjectData[0].SalePrice = obj.Prim.Properties.SalePrice;
                    properties.ObjectData[0].SaleType = (byte)obj.Prim.Properties.SaleType;
                    properties.ObjectData[0].SitName = Utils.StringToBytes(obj.Prim.Properties.SitName);
                    properties.ObjectData[0].TextureID = Utils.EmptyBytes; // FIXME: What is this?
                    properties.ObjectData[0].TouchName = Utils.StringToBytes(obj.Prim.Properties.TouchName);

                    server.UDP.SendPacket(agent.ID, properties, PacketCategory.Transaction);
                }
                else
                {
                    Logger.Log("ObjectSelect sent for missing object " + select.ObjectData[i].ObjectLocalID,
                        Helpers.LogLevel.Warning);

                    properties.ObjectData[0].Description = Utils.EmptyBytes;
                    properties.ObjectData[0].Name = Utils.EmptyBytes;
                    properties.ObjectData[0].SitName = Utils.EmptyBytes;
                    properties.ObjectData[0].TextureID = Utils.EmptyBytes;
                    properties.ObjectData[0].TouchName = Utils.EmptyBytes;

                    KillObjectPacket kill = new KillObjectPacket();
                    kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                    kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                    kill.ObjectData[0].ID = select.ObjectData[i].ObjectLocalID;
                    server.UDP.SendPacket(agent.ID, kill, PacketCategory.State);
                }
            }
            
        }

        void ObjectDeselectHandler(Packet packet, Agent agent)
        {
            ObjectDeselectPacket deselect = (ObjectDeselectPacket)packet;

            for (int i = 0; i < deselect.ObjectData.Length; i++)
            {
                uint localID = deselect.ObjectData[i].ObjectLocalID;

                SimulationObject obj;
                if (server.Scene.TryGetObject(localID, out obj))
                {
                    //Logger.DebugLog("Deselecting object " + obj.Prim.LocalID);
                }
            }

            // TODO: Do we need this at all?
        }

        void ObjectLinkHandler(Packet packet, Agent agent)
        {
            ObjectLinkPacket link = (ObjectLinkPacket)packet;
            List<SimulationObject> linkSet = new List<SimulationObject>();
            for (int i = 0; i < link.ObjectData.Length; i++)
            {
                SimulationObject obj;
                if (!server.Scene.TryGetObject(link.ObjectData[i].ObjectLocalID, out obj))
                {
                    //TODO: send an error message
                    return;
                }
                else if (obj.Prim.OwnerID != agent.ID)
                {
                    //TODO: send an error message
                    return;
                }
                else
                {
                    linkSet.Add(obj);
                }
            }

            for (int i = 0; i < linkSet.Count; i++)
            {
                linkSet[i].LinkNumber = i + 1;

                if (linkSet[i].Prim.ParentID > 0)
                {
                    //previously linked children
                    SimulationObject parent;
                    if (server.Scene.TryGetObject(linkSet[i].Prim.ParentID, out parent))
                    {
                        //re-add old root orientation
                        linkSet[i].Prim.Position = parent.Prim.Position + Vector3.Transform(linkSet[i].Prim.Position, Matrix4.CreateFromQuaternion(parent.Prim.Rotation));
                        linkSet[i].Prim.Rotation *= parent.Prim.Rotation;
                    }
                }

                if (i > 0)
                {
                    //subtract root prim orientation
                    linkSet[i].Prim.Position = Vector3.Transform(linkSet[i].Prim.Position - linkSet[0].Prim.Position, Matrix4.CreateFromQuaternion(Quaternion.Identity / linkSet[0].Prim.Rotation));
                    linkSet[i].Prim.Rotation /= linkSet[0].Prim.Rotation;

                    //set parent ID
                    linkSet[i].Prim.ParentID = linkSet[0].Prim.LocalID;
                }
                else
                {
                    //root prim
                    linkSet[i].Prim.ParentID = 0;
                }

                server.Scene.ObjectAddOrUpdate(this, linkSet[i], agent.ID, 0, linkSet[i].Prim.Flags);
            }
        }

        void ObjectDelinkHandler(Packet packet, Agent agent)
        {
            ObjectDelinkPacket delink = (ObjectDelinkPacket)packet;

            List<SimulationObject> linkSet = new List<SimulationObject>();
            for (int i = 0; i < delink.ObjectData.Length; i++)
            {
                SimulationObject obj;
                if (!server.Scene.TryGetObject(delink.ObjectData[i].ObjectLocalID, out obj))
                {
                    //TODO: send an error message
                    return;
                }
                else if (obj.Prim.OwnerID != agent.ID)
                {
                    //TODO: send an error message
                    return;
                }
                else
                {
                    linkSet.Add(obj);
                }
            }

            for (int i = 0; i < linkSet.Count; i++)
            {
                linkSet[i].Prim.ParentID = 0;
                linkSet[i].LinkNumber = 0;

                //add root prim orientation to child prims
                if (i > 0)
                {
                    linkSet[i].Prim.Position = linkSet[0].Prim.Position + Vector3.Transform(linkSet[i].Prim.Position, Matrix4.CreateFromQuaternion(linkSet[0].Prim.Rotation));
                    linkSet[i].Prim.Rotation *= linkSet[0].Prim.Rotation;
                }

                server.Scene.ObjectAddOrUpdate(this, linkSet[i], agent.ID, 0, linkSet[i].Prim.Flags);
            }
        }

        void ObjectShapeHandler(Packet packet, Agent agent)
        {
            ObjectShapePacket shape = (ObjectShapePacket)packet;

            for (int i = 0; i < shape.ObjectData.Length; i++)
            {
                ObjectShapePacket.ObjectDataBlock block = shape.ObjectData[i];

                SimulationObject obj;
                if (server.Scene.TryGetObject(block.ObjectLocalID, out obj))
                {
                    Primitive.ConstructionData data = obj.Prim.PrimData;

                    data.PathBegin = Primitive.UnpackBeginCut(block.PathBegin);
                    data.PathCurve = (PathCurve)block.PathCurve;
                    data.PathEnd = Primitive.UnpackEndCut(block.PathEnd);
                    data.PathRadiusOffset = Primitive.UnpackPathTwist(block.PathRadiusOffset);
                    data.PathRevolutions = Primitive.UnpackPathRevolutions(block.PathRevolutions);
                    data.PathScaleX = Primitive.UnpackPathScale(block.PathScaleX);
                    data.PathScaleY = Primitive.UnpackPathScale(block.PathScaleY);
                    data.PathShearX = Primitive.UnpackPathShear((sbyte)block.PathShearX);
                    data.PathShearY = Primitive.UnpackPathShear((sbyte)block.PathShearY);
                    data.PathSkew = Primitive.UnpackPathTwist(block.PathSkew);
                    data.PathTaperX = Primitive.UnpackPathTaper(block.PathTaperX);
                    data.PathTaperY = Primitive.UnpackPathTaper(block.PathTaperY);
                    data.PathTwist = Primitive.UnpackPathTwist(block.PathTwist);
                    data.PathTwistBegin = Primitive.UnpackPathTwist(block.PathTwistBegin);
                    data.ProfileBegin = Primitive.UnpackBeginCut(block.ProfileBegin);
                    data.profileCurve = block.ProfileCurve;
                    data.ProfileEnd = Primitive.UnpackEndCut(block.ProfileEnd);
                    data.ProfileHollow = Primitive.UnpackProfileHollow(block.ProfileHollow);

                    server.Scene.ObjectModify(this, obj, data);
                }
                else
                {
                    Logger.Log("Got an ObjectShape packet for unknown object " + block.ObjectLocalID,
                        Helpers.LogLevel.Warning);
                }
            }
        }

        void ObjectFlagUpdateHandler(Packet packet, Agent agent)
        {
            ObjectFlagUpdatePacket update = (ObjectFlagUpdatePacket)packet;

            SimulationObject obj;
            if (server.Scene.TryGetObject(update.AgentData.ObjectLocalID, out obj))
            {
                PrimFlags flags = obj.Prim.Flags;

                if (update.AgentData.CastsShadows)
                    flags |= PrimFlags.CastShadows;
                else
                    flags &= ~PrimFlags.CastShadows;

                if (update.AgentData.IsPhantom)
                    flags |= PrimFlags.Phantom;
                else
                    flags &= ~PrimFlags.Phantom;

                if (update.AgentData.IsTemporary)
                    flags |= PrimFlags.Temporary;
                else
                    flags &= ~PrimFlags.Temporary;

                if (update.AgentData.UsePhysics)
                    flags |= PrimFlags.Physics;
                else
                    flags &= ~PrimFlags.Physics;

                server.Scene.ObjectFlags(this, obj, flags);
            }
            else
            {
                Logger.Log("Got an ObjectFlagUpdate packet for unknown object " + update.AgentData.ObjectLocalID,
                    Helpers.LogLevel.Warning);
            }
        }

        void ObjectExtraParamsHandler(Packet packet, Agent agent)
        {
            ObjectExtraParamsPacket extra = (ObjectExtraParamsPacket)packet;

            for (int i = 0; i < extra.ObjectData.Length; i++)
            {
                ObjectExtraParamsPacket.ObjectDataBlock block = extra.ObjectData[i];

                SimulationObject obj;
                if (server.Scene.TryGetObject(block.ObjectLocalID, out obj))
                {
                    ExtraParamType type = (ExtraParamType)block.ParamType;
                    
                    if (block.ParamInUse)
                    {
                        switch (type)
                        {
                            case ExtraParamType.Flexible:
                                obj.Prim.Flexible = new Primitive.FlexibleData(block.ParamData, 0);
                                break;
                            case ExtraParamType.Light:
                                obj.Prim.Light = new Primitive.LightData(block.ParamData, 0);
                                break;
                            case ExtraParamType.Sculpt:
                                obj.Prim.Sculpt = new Primitive.SculptData(block.ParamData, 0);
                                break;
                        }
                    }
                    else
                    {
                        switch (type)
                        {
                            case ExtraParamType.Flexible:
                                obj.Prim.Flexible = null;
                                break;
                            case ExtraParamType.Light:
                                obj.Prim.Light = null;
                                break;
                            case ExtraParamType.Sculpt:
                                obj.Prim.Sculpt = null;
                                break;
                        }
                    }

                    server.Scene.ObjectAddOrUpdate(this, obj, obj.Prim.OwnerID, 0, PrimFlags.None);
                }
            }
        }

        void ObjectImageHandler(Packet packet, Agent agent)
        {
            ObjectImagePacket image = (ObjectImagePacket)packet;

            for (int i = 0; i < image.ObjectData.Length; i++)
            {
                SimulationObject obj;
                if (server.Scene.TryGetObject(image.ObjectData[i].ObjectLocalID, out obj))
                    server.Scene.ObjectModifyTextures(this, obj,
                        Utils.BytesToString(image.ObjectData[i].MediaURL),
                        new Primitive.TextureEntry(image.ObjectData[i].TextureEntry, 0, image.ObjectData[i].TextureEntry.Length));
            }
        }

        void UndoHandler(Packet packet, Agent agent)
        {
            UndoPacket undo = (UndoPacket)packet;

            for (int i = 0; i < undo.ObjectData.Length; i++)
            {
                SimulationObject obj;
                if (server.Scene.TryGetObject(undo.ObjectData[i].ObjectID, out obj))
                    server.Scene.ObjectUndo(this, obj);
            }
        }

        void RedoHandler(Packet packet, Agent agent)
        {
            RedoPacket redo = (RedoPacket)packet;

            for (int i = 0; i < redo.ObjectData.Length; i++)
            {
                SimulationObject obj;
                if (server.Scene.TryGetObject(redo.ObjectData[i].ObjectID, out obj))
                    server.Scene.ObjectRedo(this, obj);
            }
        }

        void DeRezObjectHandler(Packet packet, Agent agent)
        {
            DeRezObjectPacket derez = (DeRezObjectPacket)packet;
            DeRezDestination destination = (DeRezDestination)derez.AgentBlock.Destination;
            
            // TODO: Check permissions
            for (int i = 0; i < derez.ObjectData.Length; i++)
            {
                uint localID = derez.ObjectData[i].ObjectLocalID;

                SimulationObject obj;
                if (server.Scene.TryGetObject(localID, out obj))
                {
                    switch (destination)
                    {
                        case DeRezDestination.AgentInventorySave:
                            Logger.Log("DeRezObject: Got an AgentInventorySave, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.AgentInventoryCopy:
                            Logger.Log("DeRezObject: Got an AgentInventorySave, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.TaskInventory:
                            Logger.Log("DeRezObject: Got a TaskInventory, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.Attachment:
                            Logger.Log("DeRezObject: Got an Attachment, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.AgentInventoryTake:
                            Logger.Log("DeRezObject: Got an AgentInventoryTake, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.ForceToGodInventory:
                            Logger.Log("DeRezObject: Got a ForceToGodInventory, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.TrashFolder:
                            InventoryObject invObj;
                            if (server.Inventory.TryGetInventory(agent.ID, derez.AgentBlock.DestinationID, out invObj) &&
                                invObj is InventoryFolder)
                            {
                                // FIXME: Handle children
                                InventoryFolder trash = (InventoryFolder)invObj;
                                server.Inventory.CreateItem(agent.ID, obj.Prim.Properties.Name, obj.Prim.Properties.Description, InventoryType.Object,
                                    AssetType.Object, obj.Prim.ID, trash.ID, PermissionMask.All, PermissionMask.All, agent.ID,
                                    obj.Prim.Properties.CreatorID, derez.AgentBlock.TransactionID, 0, true);
                                server.Scene.ObjectRemove(this, obj.Prim.LocalID);

                                Logger.DebugLog(String.Format("Derezzed prim {0} to agent inventory trash", obj.Prim.LocalID));
                            }
                            else
                            {
                                Logger.Log("DeRezObject: Got a TrashFolder with an invalid trash folder: " +
                                    derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            }
                            break;
                        case DeRezDestination.AttachmentToInventory:
                            Logger.Log("DeRezObject: Got an AttachmentToInventory, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.AttachmentExists:
                            Logger.Log("DeRezObject: Got an AttachmentExists, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.ReturnToOwner:
                            Logger.Log("DeRezObject: Got a ReturnToOwner, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                        case DeRezDestination.ReturnToLastOwner:
                            Logger.Log("DeRezObject: Got a ReturnToLastOwner, DestID: " +
                                derez.AgentBlock.DestinationID.ToString(), Helpers.LogLevel.Warning);
                            break;
                    }
                }
            }
        }

        void MultipleObjectUpdateHandler(Packet packet, Agent agent)
        {
            MultipleObjectUpdatePacket update = (MultipleObjectUpdatePacket)packet;

            for (int i = 0; i < update.ObjectData.Length; i++)
            {
                bool scaled = false;
                MultipleObjectUpdatePacket.ObjectDataBlock block = update.ObjectData[i];

                SimulationObject obj;
                if (server.Scene.TryGetObject(block.ObjectLocalID, out obj))
                {
                    UpdateType type = (UpdateType)block.Type;
                    //bool linked = ((type & UpdateType.Linked) != 0);
                    int pos = 0;
                    Vector3 position = obj.Prim.Position;
                    Quaternion rotation = obj.Prim.Rotation;
                    Vector3 scale = obj.Prim.Scale;

                    if ((type & UpdateType.Position) != 0)
                    {
                        position = new Vector3(block.Data, pos);
                        pos += 12;
                    }
                    if ((type & UpdateType.Rotation) != 0)
                    {
                        rotation = new Quaternion(block.Data, pos, true);
                        pos += 12;
                    }
                    if ((type & UpdateType.Scale) != 0)
                    {
                        scaled = true;
                        scale = new Vector3(block.Data, pos);
                        pos += 12;

                        // FIXME: Use this in linksets
                        //bool uniform = ((type & UpdateType.Uniform) != 0);
                    }

                    if (scaled)
                    {
                        obj.Prim.Position = position;
                        obj.Prim.Rotation = rotation;
                        obj.Prim.Scale = scale;

                        server.Scene.ObjectAddOrUpdate(this, obj, agent.ID, 0, PrimFlags.None);
                    }
                    else
                    {
                        server.Scene.ObjectTransform(this, obj, position, rotation,
                            obj.Prim.Velocity, obj.Prim.Acceleration, obj.Prim.AngularVelocity);
                    }
                }
                else
                {
                    // Ghosted prim, send a kill message to this agent
                    KillObjectPacket kill = new KillObjectPacket();
                    kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
                    kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
                    kill.ObjectData[0].ID = block.ObjectLocalID;
                    server.UDP.SendPacket(agent.ID, kill, PacketCategory.State);
                }
            }
        }

        void RequestObjectPropertiesFamilyHandler(Packet packet, Agent agent)
        {
            RequestObjectPropertiesFamilyPacket request = (RequestObjectPropertiesFamilyPacket)packet;
            ReportType type = (ReportType)request.ObjectData.RequestFlags;

            SimulationObject obj;
            if (server.Scene.TryGetObject(request.ObjectData.ObjectID, out obj))
            {
                ObjectPropertiesFamilyPacket props = new ObjectPropertiesFamilyPacket();
                props.ObjectData.BaseMask = (uint)obj.Prim.Properties.Permissions.BaseMask;
                props.ObjectData.Category = (uint)obj.Prim.Properties.Category;
                props.ObjectData.Description = Utils.StringToBytes(obj.Prim.Properties.Description);
                props.ObjectData.EveryoneMask = (uint)obj.Prim.Properties.Permissions.EveryoneMask;
                props.ObjectData.GroupID = obj.Prim.Properties.GroupID;
                props.ObjectData.GroupMask = (uint)obj.Prim.Properties.Permissions.GroupMask;
                props.ObjectData.LastOwnerID = obj.Prim.Properties.LastOwnerID;
                props.ObjectData.Name = Utils.StringToBytes(obj.Prim.Properties.Name);
                props.ObjectData.NextOwnerMask = (uint)obj.Prim.Properties.Permissions.NextOwnerMask;
                props.ObjectData.ObjectID = obj.Prim.ID;
                props.ObjectData.OwnerID = obj.Prim.Properties.OwnerID;
                props.ObjectData.OwnerMask = (uint)obj.Prim.Properties.Permissions.OwnerMask;
                props.ObjectData.OwnershipCost = obj.Prim.Properties.OwnershipCost;
                props.ObjectData.RequestFlags = (uint)type;
                props.ObjectData.SalePrice = obj.Prim.Properties.SalePrice;
                props.ObjectData.SaleType = (byte)obj.Prim.Properties.SaleType;

                server.UDP.SendPacket(agent.ID, props, PacketCategory.Transaction);
            }
            else
            {
                Logger.Log("RequestObjectPropertiesFamily sent for unknown object " +
                    request.ObjectData.ObjectID.ToString(), Helpers.LogLevel.Warning);
            }
        }

        Vector3 FullSceneCollisionTest(Vector3 rayStart, Vector3 rayEnd)
        {
            // HACK: For now
            Logger.DebugLog("Full scene collision test was requested, ignoring");
            return rayEnd;
        }
    }
}
