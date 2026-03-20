using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.Download;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerToolType]
    public static class McpServer
    {
        private static IServiceProvider? _services;
        private static Portal? _portal;

        public static ILogger? Logger { get; set; }

        public static Portal Portal
        {
            get
            {
                if (_services !=null)
                {
                    return _services.GetRequiredService<Portal>();
                }
                else
                {
                    if (_portal == null)
                    {
                        _portal = new Portal();
                    }
                    return _portal;
                }
            }
            set
            {
                _portal = value ?? throw new ArgumentNullException(nameof(value), "Portal cannot be null");
            }
        }

        public static void SetServiceProvider(IServiceProvider services)
        {
            _services = services;
        }

        #region portal

        [McpServerTool(Name = "Connect"), Description("Connect to TIA-Portal")]
        public static ResponseConnect Connect()
        {
            Logger?.LogInformation("Connecting to TIA Portal...");

            try
            {
                if (Portal.ConnectPortal())
                {
                    return new ResponseConnect
                    {
                        Message = "Connected to TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to connect to TIA-Portal", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting to TIA-Portal: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "Disconnect"), Description("Disconnect from TIA-Portal")]
        public static ResponseDisconnect Disconnect()
        {
            try
            {
                if (Portal.DisconnectPortal())
                {
                    return new ResponseDisconnect
                    {
                        Message = "Disconnected from TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed disconnecting from TIA-Portal", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error disconnecting from TIA-Portal: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region state

        [McpServerTool(Name = "GetState"), Description("Get the state of the TIA-Portal MCP server")]
        public static ResponseState GetState()
        {
            try
            {
                var state = Portal.GetState();

                if (state != null)
                {
                    return new ResponseState
                    {
                        Message = "TIA-Portal MCP server state retrieved",
                        IsConnected = state.IsConnected,
                        Project = state.Project,
                        Session = state.Session,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to retrieve TIA-Portal MCP server state", McpErrorCode.InternalError);
                }
                

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving TIA-Portal MCP server state: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region project/session

        [McpServerTool(Name = "GetProject"), Description("Get open local project/session")]
        public static ResponseGetProjects GetProjects()
        {
            try
            {
                var list = Portal.GetProjects();

                list.AddRange(Portal.GetSessions());

                var responseList = new List<ResponseProjectInfo>();
                foreach (var project in list)
                {
                    var attributes = Helper.GetAttributeList(project);

                    if (project != null)
                    {
                        responseList.Add(new ResponseProjectInfo
                        {
                            Name = project.Name,
                            Attributes = attributes
                        });
                    }
                }

                return new ResponseGetProjects
                {
                    Message = "Open projects and sessions retrieved",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving open projects: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "OpenProject"), Description("Open a TIA-Portal local project/session")]
        public static ResponseOpenProject OpenProject(
            [Description("path: defines the path where to the project/session")] string path)
        {
            try
            {
                Portal.CloseProject();

                // get project extension
                string extension = Path.GetExtension(path).ToLowerInvariant();

                // use regex to check if extension is .ap\d+ or .als\d+
                if (!Regex.IsMatch(extension, @"^\.ap\d+$") &&
                    !Regex.IsMatch(extension, @"^\.als\d+$"))
                {
                    throw new McpException("Invalid project file extension. Use .apXX for projects or .alsXX for sessions, where XX=18,19,20,....", McpErrorCode.InvalidParams);
                }

                bool success = false;

                if (extension.StartsWith(".ap"))
                {
                    success = Portal.OpenProject(path);
                }
                if (extension.StartsWith(".als"))
                {
                    success = Portal.OpenSession(path);
                }

                if (success)
                {
                    return new ResponseOpenProject
                    {
                        Message = $"Project '{path}' opened",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed to open project '{path}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening project '{path}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SaveProject"), Description("Save the current TIA-Portal local project/session")]
        public static ResponseSaveProject SaveProject()
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    if (Portal.SaveSession())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local session saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save local session", McpErrorCode.InternalError);
                    }
                }
                else
                {
                    if (Portal.SaveProject())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local project saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save project", McpErrorCode.InternalError);
                    }
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SaveAsProject"), Description("Save current TIA-Portal project/session with a new name")]
        public static ResponseSaveAsProject SaveAsProject(
            [Description("newProjectPath: defines the new path where to save the project")] string newProjectPath)
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    throw new McpException($"Cannot save local session as '{newProjectPath}'", McpErrorCode.InvalidParams);
                }
                else
                {
                    if (Portal.SaveAsProject(newProjectPath))
                    {
                        return new ResponseSaveAsProject
                        {
                            Message = $"Local project saved as '{newProjectPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException($"Failed saving local project as '{newProjectPath}'", McpErrorCode.InternalError);
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session as '{newProjectPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CloseProject"), Description("Close the current TIA-Portal project/session")]
        public static ResponseCloseProject CloseProject()
        {
            try
            {
                bool success;

                if (Portal.IsLocalSession)
                {
                    success = Portal.CloseSession();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local session closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing local session", McpErrorCode.InternalError);
                    }
                }
                else
                {
                    success = Portal.CloseProject();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local project closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing project", McpErrorCode.InternalError);
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error closing local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region devices

        [McpServerTool(Name = "GetProjectTree"), Description("Get project structure as a tree view on current local project/session")]
        public static ResponseProjectTree GetProjectTree()
        {
            try
            {
                var tree = Portal.GetProjectTree();

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseProjectTree
                    {
                        Message = "Project tree retrieved",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed retrieving project tree", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving project tree: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDeviceInfo"), Description("Get info from a device from the current project/session")]
        public static ResponseDeviceInfo GetDeviceInfo(
            [Description("devicePath: defines the path in the project structure to the device")] string devicePath)
        {
            try
            {
                var device = Portal.GetDevice(devicePath);

                if (device != null)
                {
                    var attributes = Helper.GetAttributeList(device);

                    return new ResponseDeviceInfo
                    {
                        Message = $"Device info retrieved from '{devicePath}'",
                        Name = device.Name,
                        Attributes = attributes,
                        Description = device.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device not found at '{devicePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device info from '{devicePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDeviceItemInfo"), Description("Get info from a device item from the current project/session")]
        public static ResponseDeviceItemInfo GetDeviceItemInfo(
            [Description("deviceItemPath: defines the path in the project structure to the device item")] string deviceItemPath)
        {
            try
            {
                var deviceItem = Portal.GetDeviceItem(deviceItemPath);

                if (deviceItem != null)
                {
                    var attributes = Helper.GetAttributeList(deviceItem);

                    return new ResponseDeviceItemInfo
                    {
                        Message = $"Device item info retrieved from '{deviceItemPath}'",
                        Name = deviceItem.Name,
                        Attributes = attributes,
                        Description = deviceItem.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Device item not found at '{deviceItemPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device item info from '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDevices"), Description("Get a list of all devices in the project/session")]
        public static ResponseDevices GetDevices()
        {
            try
            {
                var list = Portal.GetDevices();
                var responseList = new List<ResponseDeviceInfo>();

                if (list != null)
                {
                    foreach (var device in list)
                    {
                        if (device != null)
                        {
                            var attributes = Helper.GetAttributeList(device);
                            responseList.Add(new ResponseDeviceInfo
                            {
                                Name = device.Name,
                                Attributes = attributes,
                                Description = device.ToString()
                            });
                        }
                    }

                    return new ResponseDevices
                    {
                        Message = "Devices retrieved",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving devices", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving devices: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region hardware and network configuration

        [McpServerTool(Name = "CreateDevice"), Description("Create a new device (PLC, HMI, etc.) in the current project")]
        public static ResponseCreateDevice CreateDevice(
            [Description("typeIdentifier: the device type identifier, e.g. 'OrderNumber:6ES7 515-2AM02-0AB0/V2.0'")] string typeIdentifier,
            [Description("name: the name for the device item")] string name,
            [Description("deviceName: the name for the device")] string deviceName)
        {
            try
            {
                var device = Portal.CreateDevice(typeIdentifier, name, deviceName);

                return new ResponseCreateDevice
                {
                    Message = $"Device '{deviceName}' created successfully",
                    Name = device.Name,
                    TypeIdentifier = typeIdentifier,
                    DevicePath = device.Name,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to create device '{deviceName}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating device '{deviceName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteDevice"), Description("Delete a device from the current project")]
        public static ResponseDeleteDevice DeleteDevice(
            [Description("devicePath: the path to the device to delete")] string devicePath)
        {
            try
            {
                Portal.DeleteDevice(devicePath);

                return new ResponseDeleteDevice
                {
                    Message = $"Device at '{devicePath}' deleted successfully",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to delete device at '{devicePath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting device at '{devicePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CreateDeviceGroup"), Description("Create a device group for organizing devices in the project")]
        public static ResponseCreateDeviceGroup CreateDeviceGroup(
            [Description("groupName: the name for the new device group")] string groupName,
            [Description("parentGroupPath: optional path to the parent group (e.g. 'Group1/SubGroup1')")] string parentGroupPath = "")
        {
            try
            {
                var group = Portal.CreateDeviceGroup(groupName, parentGroupPath);

                return new ResponseCreateDeviceGroup
                {
                    Message = $"Device group '{groupName}' created successfully",
                    Name = group.Name,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to create device group '{groupName}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating device group '{groupName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetModules"), Description("Get all modules/submodules in a device item (rack)")]
        public static ResponseModules GetModules(
            [Description("deviceItemPath: the path to the device item (e.g. 'PLC_1/Rack_0')")] string deviceItemPath)
        {
            try
            {
                var modules = Portal.GetModules(deviceItemPath);
                var responseList = new List<ResponseModule>();

                foreach (var module in modules)
                {
                    var attributes = Helper.GetAttributeList(module);
                    var typeIdentifier = "";
                    var positionNumber = 0;

                    try
                    {
                        typeIdentifier = module.GetAttribute("TypeIdentifier")?.ToString() ?? "";
                    }
                    catch { }

                    try
                    {
                        positionNumber = (int)(module.GetAttribute("PositionNumber") ?? 0);
                    }
                    catch { }

                    responseList.Add(new ResponseModule
                    {
                        Name = module.Name,
                        TypeIdentifier = typeIdentifier,
                        PositionNumber = positionNumber,
                        Attributes = attributes
                    });
                }

                return new ResponseModules
                {
                    Message = $"Modules retrieved for '{deviceItemPath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = responseList.Count
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to get modules for '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting modules for '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetModuleInfo"), Description("Get detailed info about a specific module/device item")]
        public static ResponseModuleInfo GetModuleInfo(
            [Description("deviceItemPath: the path to the device item/module")] string deviceItemPath)
        {
            try
            {
                var deviceItem = Portal.GetModuleInfo(deviceItemPath);
                var attributes = Helper.GetAttributeList(deviceItem);
                var typeIdentifier = "";
                var positionNumber = 0;

                try
                {
                    typeIdentifier = deviceItem.GetAttribute("TypeIdentifier")?.ToString() ?? "";
                }
                catch { }

                try
                {
                    positionNumber = (int)(deviceItem.GetAttribute("PositionNumber") ?? 0);
                }
                catch { }

                return new ResponseModuleInfo
                {
                    Message = $"Module info retrieved for '{deviceItemPath}'",
                    Name = deviceItem.Name,
                    TypeIdentifier = typeIdentifier,
                    PositionNumber = positionNumber,
                    Attributes = attributes,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to get module info for '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting module info for '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetAddresses"), Description("Get I/O addresses of a module/device item")]
        public static ResponseAddresses GetAddresses(
            [Description("deviceItemPath: the path to the device item/module")] string deviceItemPath)
        {
            try
            {
                var addresses = Portal.GetAddresses(deviceItemPath);
                var responseList = new List<ResponseAddress>();

                foreach (var (startAddress, length, ioType) in addresses)
                {
                    responseList.Add(new ResponseAddress
                    {
                        StartAddress = startAddress,
                        Length = length,
                        IoType = ioType
                    });
                }

                return new ResponseAddresses
                {
                    Message = $"Addresses retrieved for '{deviceItemPath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = responseList.Count
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to get addresses for '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting addresses for '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetSubnets"), Description("Get all subnets in the current project")]
        public static ResponseSubnets GetSubnets()
        {
            try
            {
                var subnets = Portal.GetSubnets();
                var responseList = new List<ResponseSubnet>();

                foreach (var subnet in subnets)
                {
                    var subnetType = "";

                    try
                    {
                        subnetType = subnet.GetAttribute("Type")?.ToString() ?? "";
                    }
                    catch { }

                    responseList.Add(new ResponseSubnet
                    {
                        Name = subnet.Name,
                        SubnetType = subnetType,
                        Id = subnet.ToString()
                    });
                }

                return new ResponseSubnets
                {
                    Message = "Subnets retrieved",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = responseList.Count
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to get subnets: {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting subnets: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetNetworkInterfaces"), Description("Get network interfaces of a device")]
        public static ResponseNetworkInterfaces GetNetworkInterfaces(
            [Description("deviceItemPath: the path to the device item to inspect for network interfaces")] string deviceItemPath)
        {
            try
            {
                var interfaces = Portal.GetNetworkInterfaces(deviceItemPath);
                var responseList = new List<ResponseNetworkInterface>();

                foreach (var (name, interfaceType, ipAddress, subnetMask) in interfaces)
                {
                    responseList.Add(new ResponseNetworkInterface
                    {
                        Name = name,
                        InterfaceType = interfaceType,
                        IpAddress = ipAddress,
                        SubnetMask = subnetMask
                    });
                }

                return new ResponseNetworkInterfaces
                {
                    Message = $"Network interfaces retrieved for '{deviceItemPath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = responseList.Count
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to get network interfaces for '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting network interfaces for '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SetIpAddress"), Description("Set IP address configuration on a device network interface")]
        public static ResponseSetIpAddress SetIpAddress(
            [Description("deviceItemPath: the path to the device item with the network interface")] string deviceItemPath,
            [Description("ipAddress: the IP address to set (e.g. '192.168.0.1')")] string ipAddress,
            [Description("subnetMask: the subnet mask (e.g. '255.255.255.0')")] string subnetMask,
            [Description("routerAddress: optional router/gateway address")] string routerAddress = "")
        {
            try
            {
                Portal.SetIpAddress(deviceItemPath, ipAddress, subnetMask, routerAddress);

                return new ResponseSetIpAddress
                {
                    Message = $"IP address set to '{ipAddress}' on '{deviceItemPath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to set IP address on '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error setting IP address on '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ConnectToSubnet"), Description("Connect a device network interface to a subnet")]
        public static ResponseConnectToSubnet ConnectToSubnet(
            [Description("deviceItemPath: the path to the device item with the network interface")] string deviceItemPath,
            [Description("subnetName: the name of the subnet to connect to")] string subnetName)
        {
            try
            {
                Portal.ConnectToSubnet(deviceItemPath, subnetName);

                return new ResponseConnectToSubnet
                {
                    Message = $"Device '{deviceItemPath}' connected to subnet '{subnetName}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to connect '{deviceItemPath}' to subnet '{subnetName}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting '{deviceItemPath}' to subnet '{subnetName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportGsdFile"), Description("Import a GSD/GSDML file into the current project")]
        public static ResponseImportGsdFile ImportGsdFile(
            [Description("gsdFilePath: the full file path to the GSD/GSDML file to import")] string gsdFilePath)
        {
            try
            {
                Portal.ImportGsdFile(gsdFilePath);

                return new ResponseImportGsdFile
                {
                    Message = $"GSD file '{gsdFilePath}' imported successfully",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (PortalException pex)
            {
                throw new McpException($"Failed to import GSD file '{gsdFilePath}': {pex.Message}", pex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing GSD file '{gsdFilePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region plc software

        [McpServerTool(Name = "GetSoftwareInfo"), Description("Get plc software info")]
        public static ResponseSoftwareInfo GetSoftwareInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var software = Portal.GetPlcSoftware(softwarePath);
                if (software != null)
                {

                    var attributes = Helper.GetAttributeList(software);

                    return new ResponseSoftwareInfo
                    {
                        Message = $"Software info retrieved from '{softwarePath}'",
                        Name = software.Name,
                        Attributes = attributes,
                        Description = software.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Software not found at '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software info from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CompileSoftware"), Description("Compile the plc software")]
        public static ResponseCompileSoftware CompileSoftware(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("password: the password to access adminsitration, default: no password")] string password = "")
        {
            try
            {
                var result = Portal.CompileSoftware(softwarePath, password);
                if (result != null && !result.State.ToString().Equals("Error"))
                {
                    return new ResponseCompileSoftware
                    {
                        Message = $"Software '{softwarePath}' compiled with {result}",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed compiling software '{softwarePath}': {result}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error compiling software '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetSoftwareTree"), Description("Get the structure/tree of a given PLC software showing blocks, types, and external sources")]
        public static ResponseSoftwareTree GetSoftwareTree(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var tree = Portal.GetSoftwareTree(softwarePath);

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseSoftwareTree
                    {
                        Message = $"Software tree retrieved from '{softwarePath}'",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving software tree from '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software tree from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region blocks

        [McpServerTool(Name = "GetBlockInfo"), Description("Get a block info, which is located in the plc software")]
        public static ResponseBlockInfo GetBlockInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath)
        {
            try
            {
                var block = Portal.GetBlock(softwarePath, blockPath);
                if (block != null)
                {
                    var attributes = Helper.GetAttributeList(block);

                    return new ResponseBlockInfo
                    {
                        Message = $"Block info retrieved from '{blockPath}' in '{softwarePath}'",
                        Name = block.Name,
                        TypeName = block.GetType().Name,
                        Namespace = block.Namespace,
                        ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage),block.ProgrammingLanguage),
                        MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                        IsConsistent = block.IsConsistent,
                        HeaderName = block.HeaderName,
                        ModifiedDate = block.ModifiedDate,
                        IsKnowHowProtected = block.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = block.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Block not found at '{blockPath}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving block info from '{blockPath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlocks"), Description("Get a list of blocks, which are located in plc software")]
        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetBlocks(softwarePath, regexName);

                var responseList = new List<ResponseBlockInfo>();
                foreach (var block in list)
                {
                    if (block != null)
                    {
                        var attributes = Helper.GetAttributeList(block);

                        responseList.Add(new ResponseBlockInfo
                        {
                            Name = block.Name,
                            TypeName = block.GetType().Name,
                            Namespace = block.Namespace,
                            ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                            MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                            IsConsistent = block.IsConsistent,
                            HeaderName = block.HeaderName,
                            ModifiedDate = block.ModifiedDate,
                            IsKnowHowProtected = block.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = block.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseBlocks
                    {
                        Message = $"Blocks with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving blocks with regex '{regexName}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving blocks with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlocksWithHierarchy"), Description("Get a list of all blocks with their group hierarchy from the plc software.")]
        public static ResponseBlocksWithHierarchy GetBlocksWithHierarchy(
        [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var rootGroup = Portal.GetBlockRootGroup(softwarePath);
                if (rootGroup != null)
                {
                    var hierarchy = Helper.BuildBlockHierarchy(rootGroup);
                    return new ResponseBlocksWithHierarchy
                    {
                        Message = $"Block hierarchy retrieved from '{softwarePath}'",
                        Root = hierarchy,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    // Specific failure: root group could not be resolved
                    throw new McpException($"Block root group not found for '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Generic unexpected failure wrapper
                throw new McpException($"Unexpected error retrieving block hierarchy for '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }



        [McpServerTool(Name = "ExportBlock"), Description("Export a block from plc software to file")]
        public static ResponseExportBlock ExportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath,
            [Description("exportPath: defines the path where to export the block")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                var block = Portal.ExportBlock(softwarePath, blockPath, exportPath, preservePath);
                if (block != null)
                {
                    return new ResponseExportBlock
                    {
                        Message = $"Block exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                // Should not be reachable because Portal.ExportBlock throws on failure
                throw new McpException($"Failed exporting block from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                // Map known portal errors to sharper MCP errors and messages.
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var suggestionNote = string.Empty;
                            // If the path has no '/', it may be incomplete; build suggestions using Portal's regex search and path resolver
                            if (!string.IsNullOrEmpty(blockPath) && !blockPath.Contains('/'))
                            {
                                try
                                {
                                    var escaped = Regex.Escape(blockPath);
                                    var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                                    if (blocks == null || blocks.Count == 0)
                                    {
                                        blocks = Portal.GetBlocks(softwarePath, escaped);
                                    }

                                    var candidates = blocks
                                        .Take(10)
                                        .Select(b => Portal.GetBlockPath(b))
                                        .Where(p => !string.IsNullOrWhiteSpace(p))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    if (candidates.Count > 0)
                                    {
                                        suggestionNote = $" Did you mean: {string.Join(", ", candidates)}?";
                                    }
                                }
                                catch
                                {
                                    // Best-effort suggestions only
                                }
                            }

                            var msg = $"Block not found.{suggestionNote}".Trim();
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            // Relay underlying portal error with concise reason; log full details
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export block.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";

                            Logger?.LogError(pex, "MCP ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["blockPath"], pex.Data?["exportPath"]);

                            throw new McpException(msg, McpErrorCode.InternalError);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        {
                            throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                        }
                }

                // Fallback
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting block from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static string BuildBlockPathSuggestion(string softwarePath, string blockPath)
        {
            if (string.IsNullOrEmpty(blockPath) || blockPath.Contains('/')) return string.Empty;
            try
            {
                var escaped = Regex.Escape(blockPath);
                var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                if (blocks == null || blocks.Count == 0)
                {
                    blocks = Portal.GetBlocks(softwarePath, escaped);
                }

                var candidates = blocks
                    .Take(10)
                    .Select(b =>
                    {
                        var name = b.Name;
                        var parts = new List<string> { name };
                        var parent = b.Parent;
                        while (parent != null)
                        {
                            if (parent is PlcBlockSystemGroup) break;
                            if (parent is PlcBlockGroup grp)
                            {
                                parts.Insert(0, grp.Name);
                                parent = grp.Parent;
                            }
                            else break;
                        }
                        if (parts.Count > 1) parts.RemoveAt(0);
                        return string.Join("/", parts);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return candidates.Count > 0 ? $" Did you mean: {string.Join(", ", candidates)}?" : string.Empty;
            }
            catch
            {
                return string.Empty; // best effort only
            }
        }
        [McpServerTool(Name = "ImportBlock"), Description("Import a block file to plc software")]
        public static ResponseImportBlock ImportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the block")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the block")] string importPath)
        {
            try
            {
                if (Portal.ImportBlock(softwarePath, groupPath, importPath))
                {
                    return new ResponseImportBlock
                    {
                        Message = $"Block imported from '{importPath}' to '{groupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing block from '{importPath}' to '{groupPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing block from '{importPath}' to '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocks"), Description("Export all blocks from the plc software to path")]
        public static async Task<ResponseExportBlocks> ExportBlocks(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the blocks")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks from '{softwarePath}' to '{exportPath}'");
                
                var allBlocks = await Task.Run(() => Portal.GetBlocks(softwarePath, regexName));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No blocks found to export",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportBlocks
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalBlocks,
                        Message = $"Starting export of {totalBlocks} blocks...",
                        progressToken
                    });
                }

                // Export blocks asynchronously
                var exportedBlocks = await Task.Run(() => Portal.ExportBlocks(softwarePath, exportPath, regexName, preservePath));

                // Build list of inconsistent (skipped) blocks for reporting
                var inconsistentInfos = new List<ResponseBlockInfo>();
                if (allBlocks != null)
                {
                    foreach (var b in allBlocks)
                    {
                        if (b != null && b.IsConsistent == false)
                        {
                            var attrs = Helper.GetAttributeList(b);
                            inconsistentInfos.Add(new ResponseBlockInfo
                            {
                                Name = b.Name,
                                TypeName = b.GetType().Name,
                                Namespace = b.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), b.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), b.MemoryLayout),
                                IsConsistent = b.IsConsistent,
                                HeaderName = b.HeaderName,
                                ModifiedDate = b.ModifiedDate,
                                IsKnowHowProtected = b.IsKnowHowProtected,
                                Attributes = attrs,
                                Description = b.ToString()
                            });
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedBlocks != null && progressToken != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalBlocks,
                        Message = $"Exported {exportedCount} of {totalBlocks} blocks",
                        progressToken
                    });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);

                            responseList.Add(new ResponseBlockInfo
                            {
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalBlocks,
                            Message = $"Export completed: {processedCount} blocks exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocks
                    {
                        Message = $"Export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["inconsistentBlocks"] = inconsistentInfos.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteBlock"), Description("Delete a block from the plc software")]
        public static ResponseDeleteResult DeleteBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name'")] string blockPath)
        {
            try
            {
                Portal.DeleteBlock(softwarePath, blockPath);

                return new ResponseDeleteResult
                {
                    Success = true,
                    Name = blockPath.Contains("/") ? blockPath.Substring(blockPath.LastIndexOf("/") + 1) : blockPath,
                    Path = blockPath,
                    Message = $"Block '{blockPath}' deleted from '{softwarePath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Block '{blockPath}' not found in '{softwarePath}'. {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to delete block '{blockPath}' in '{softwarePath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting block '{blockPath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CopyBlock"), Description("Copy a block to another group in the plc software")]
        public static ResponseCopyBlock CopyBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourceBlockPath: full path to the source block, e.g. 'Group/Subgroup/Name'")] string sourceBlockPath,
            [Description("targetGroupPath: path to the target group where the block will be copied, e.g. 'Group/Subgroup'")] string targetGroupPath)
        {
            try
            {
                var block = Portal.CopyBlock(softwarePath, sourceBlockPath, targetGroupPath);

                if (block != null)
                {
                    return new ResponseCopyBlock
                    {
                        Message = $"Block '{sourceBlockPath}' copied to '{targetGroupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed copying block '{sourceBlockPath}' to '{targetGroupPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to copy block '{sourceBlockPath}' to '{targetGroupPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error copying block '{sourceBlockPath}' to '{targetGroupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "MoveBlock"), Description("Move a block to another group in the plc software")]
        public static ResponseMoveBlock MoveBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourceBlockPath: full path to the source block, e.g. 'Group/Subgroup/Name'")] string sourceBlockPath,
            [Description("targetGroupPath: path to the target group where the block will be moved, e.g. 'Group/Subgroup'")] string targetGroupPath)
        {
            try
            {
                Portal.MoveBlock(softwarePath, sourceBlockPath, targetGroupPath);

                return new ResponseMoveBlock
                {
                    Message = $"Block '{sourceBlockPath}' moved to '{targetGroupPath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to move block '{sourceBlockPath}' to '{targetGroupPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error moving block '{sourceBlockPath}' to '{targetGroupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CreateBlockGroup"), Description("Create a new block group/folder in the plc software")]
        public static ResponseCreateBlockGroup CreateBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: path to the parent group where the new group will be created, use empty string for root")] string parentGroupPath,
            [Description("groupName: name of the new block group to create")] string groupName)
        {
            try
            {
                var group = Portal.CreateBlockGroup(softwarePath, parentGroupPath, groupName);

                if (group != null)
                {
                    var path = string.IsNullOrEmpty(parentGroupPath) ? groupName : $"{parentGroupPath}/{groupName}";

                    return new ResponseCreateBlockGroup
                    {
                        Name = groupName,
                        Path = path,
                        Message = $"Block group '{groupName}' created in '{parentGroupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed creating block group '{groupName}' in '{parentGroupPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to create block group '{groupName}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating block group '{groupName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteBlockGroup"), Description("Delete an empty block group from the plc software")]
        public static ResponseDeleteResult DeleteBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: path to the block group to delete, e.g. 'Group/Subgroup'")] string groupPath)
        {
            try
            {
                Portal.DeleteBlockGroup(softwarePath, groupPath);

                return new ResponseDeleteResult
                {
                    Success = true,
                    Name = groupPath.Contains("/") ? groupPath.Substring(groupPath.LastIndexOf("/") + 1) : groupPath,
                    Path = groupPath,
                    Message = $"Block group '{groupPath}' deleted from '{softwarePath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to delete block group '{groupPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting block group '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlockGroup"), Description("Get block group info with contents summary from the plc software")]
        public static ResponseBlockGroupInfo GetBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: path to the block group, use empty string for root group")] string groupPath = "")
        {
            try
            {
                var (name, path, blockCount, subGroupCount) = Portal.GetBlockGroupInfo(softwarePath, groupPath);

                return new ResponseBlockGroupInfo
                {
                    Name = name,
                    Path = path,
                    BlockCount = blockCount,
                    SubGroupCount = subGroupCount,
                    Message = $"Block group info retrieved for '{groupPath}' in '{softwarePath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to get block group info for '{groupPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting block group info for '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region types

        [McpServerTool(Name = "GetTypeInfo"), Description("Get a type info from the plc software")]
        public static ResponseTypeInfo GetTypeInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath)
        {
            try
            {
                var type = Portal.GetType(softwarePath, typePath);
                if (type != null)
                {
                    var attributes = Helper.GetAttributeList(type);

                    return new ResponseTypeInfo
                    {
                        Message = $"Type info retrieved from '{typePath}' in '{softwarePath}'",
                        Name = type.Name,
                        TypeName = type.GetType().Name,
                        Namespace = type.Namespace,
                        IsConsistent = type.IsConsistent,
                        ModifiedDate = type.ModifiedDate,
                        IsKnowHowProtected = type.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = type.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Type not found at '{typePath}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving type info from '{typePath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetTypes"), Description("Get a list of types from the plc software")]
        public static ResponseTypes GetTypes(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetTypes(softwarePath, regexName);

                var responseList = new List<ResponseTypeInfo>();
                foreach (var type in list)
                {
                    if (type != null)
                    {
                        var attributes = Helper.GetAttributeList(type);

                        responseList.Add(new ResponseTypeInfo
                        {
                            Name = type.Name,
                            TypeName = type.GetType().Name,
                            Namespace = type.Namespace,
                            IsConsistent = type.IsConsistent,
                            ModifiedDate = type.ModifiedDate,
                            IsKnowHowProtected = type.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = type.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseTypes
                    {
                        Message = $"Types with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving user defined types with regex '{regexName}' in '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving user defined types with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportType"), Description("Export a type from the plc software")]
        public static ResponseExportType ExportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where export the type")] string exportPath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                var type = Portal.ExportType(softwarePath, typePath, exportPath, preservePath);
                if (type != null)
                {
                    return new ResponseExportType
                    {
                        Message = $"Type exported from '{typePath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting type from '{typePath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException("Type not found.", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export type.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportType failed for {SoftwarePath} {TypePath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["typePath"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting type from '{typePath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportType"), Description("Import a type from file into the plc software")]
        public static ResponseImportType ImportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the type")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the type")] string importPath)
        {
            try
            {
                if (Portal.ImportType(softwarePath, groupPath, importPath))
                {
                    return new ResponseImportType
                    {
                        Message = $"Type imported from '{importPath}' to '{groupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing type from '{importPath}' to '{groupPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing type from '{importPath}' to '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportTypes"), Description("Export types from the plc software to path")]
        public static async Task<ResponseExportTypes> ExportTypes(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the types")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                // First, get the list of types to determine total count
                Logger?.LogInformation($"Starting export of types from '{softwarePath}' to '{exportPath}'");
                
                var allTypes = await Task.Run(() => Portal.GetTypes(softwarePath, regexName));
                var totalTypes = allTypes?.Count ?? 0;

                if (totalTypes == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No types found to export",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportTypes
                    {
                        Message = $"No types found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseTypeInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = 0,
                            ["exportedTypes"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalTypes,
                        Message = $"Starting export of {totalTypes} types...",
                        progressToken
                    });
                }

                // Export types asynchronously
                var exportedTypes = await Task.Run(() => Portal.ExportTypes(softwarePath, exportPath, regexName, preservePath));

                // Build list of inconsistent (skipped) types for reporting
                var inconsistentTypeInfos = new List<ResponseTypeInfo>();
                if (allTypes != null)
                {
                    foreach (var t in allTypes)
                    {
                        if (t != null && t.IsConsistent == false)
                        {
                            var attrs = Helper.GetAttributeList(t);
                            inconsistentTypeInfos.Add(new ResponseTypeInfo
                            {
                                Name = t.Name,
                                TypeName = t.GetType().Name,
                                Namespace = t.Namespace,
                                IsConsistent = t.IsConsistent,
                                ModifiedDate = t.ModifiedDate,
                                IsKnowHowProtected = t.IsKnowHowProtected,
                                Attributes = attrs,
                                Description = t.ToString()
                            });
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedTypes != null && progressToken != null)
                {
                    var exportedCount = exportedTypes.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalTypes,
                        Message = $"Exported {exportedCount} of {totalTypes} types",
                        progressToken
                    });
                }

                if (exportedTypes != null)
                {
                    var responseList = new List<ResponseTypeInfo>();
                    var processedCount = 0;
                    
                    foreach (var type in exportedTypes)
                    {
                        if (type != null)
                        {
                            var attributes = Helper.GetAttributeList(type);

                            responseList.Add(new ResponseTypeInfo
                            {
                                Name = type.Name,
                                TypeName = type.GetType().Name,
                                Namespace = type.Namespace,
                                IsConsistent = type.IsConsistent,
                                ModifiedDate = type.ModifiedDate,
                                IsKnowHowProtected = type.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = type.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalTypes,
                            Message = $"Export completed: {processedCount} types exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Type export completed: {processedCount} types exported in {duration:F2} seconds");

                    return new ResponseExportTypes
                    {
                        Message = $"Export completed: {processedCount} types with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentTypeInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = totalTypes,
                            ["exportedTypes"] = processedCount,
                            ["inconsistentTypes"] = inconsistentTypeInfos.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Type export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting types '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteType"), Description("Delete a PLC type from the plc software")]
        public static ResponseDeleteResult DeleteType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: full path to the type in the project structure, e.g. 'Group/Subgroup/Name'")] string typePath)
        {
            try
            {
                Portal.DeleteType(softwarePath, typePath);

                return new ResponseDeleteResult
                {
                    Success = true,
                    Name = typePath.Contains("/") ? typePath.Substring(typePath.LastIndexOf("/") + 1) : typePath,
                    Path = typePath,
                    Message = $"Type '{typePath}' deleted from '{softwarePath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Type '{typePath}' not found in '{softwarePath}'. {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to delete type '{typePath}' in '{softwarePath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting type '{typePath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CreateTypeGroup"), Description("Create a new type group/folder in the plc software")]
        public static ResponseCreateTypeGroup CreateTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("parentGroupPath: path to the parent group where the new group will be created, use empty string for root")] string parentGroupPath,
            [Description("groupName: name of the new type group to create")] string groupName)
        {
            try
            {
                var group = Portal.CreateTypeGroup(softwarePath, parentGroupPath, groupName);

                if (group != null)
                {
                    var path = string.IsNullOrEmpty(parentGroupPath) ? groupName : $"{parentGroupPath}/{groupName}";

                    return new ResponseCreateTypeGroup
                    {
                        Name = groupName,
                        Path = path,
                        Message = $"Type group '{groupName}' created in '{parentGroupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed creating type group '{groupName}' in '{parentGroupPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to create type group '{groupName}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating type group '{groupName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteTypeGroup"), Description("Delete an empty type group from the plc software")]
        public static ResponseDeleteResult DeleteTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: path to the type group to delete, e.g. 'Group/Subgroup'")] string groupPath)
        {
            try
            {
                Portal.DeleteTypeGroup(softwarePath, groupPath);

                return new ResponseDeleteResult
                {
                    Success = true,
                    Name = groupPath.Contains("/") ? groupPath.Substring(groupPath.LastIndexOf("/") + 1) : groupPath,
                    Path = groupPath,
                    Message = $"Type group '{groupPath}' deleted from '{softwarePath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to delete type group '{groupPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting type group '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetTypeGroup"), Description("Get type group info with contents summary from the plc software")]
        public static ResponseTypeGroupInfo GetTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: path to the type group, use empty string for root group")] string groupPath = "")
        {
            try
            {
                var (name, path, typeCount, subGroupCount) = Portal.GetTypeGroupInfo(softwarePath, groupPath);

                return new ResponseTypeGroupInfo
                {
                    Name = name,
                    Path = path,
                    TypeCount = typeCount,
                    SubGroupCount = subGroupCount,
                    Message = $"Type group info retrieved for '{groupPath}' in '{softwarePath}'",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"{pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed to get type group info for '{groupPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting type group info for '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region documents

        [McpServerTool(Name = "ExportAsDocuments"), Description("Export as documents (.s7dcl/.s7res) from a block in the plc software to path")]
        public static ResponseExportAsDocuments ExportAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportAsDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }
                if (Portal.ExportAsDocuments(softwarePath, blockPath, exportPath, preservePath))
                {
                    return new ResponseExportAsDocuments
                    {
                        Message = $"Documents exported from '{blockPath}' to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting documents from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocksAsDocuments"), Description("Export as documents (.s7dcl/.s7res) from blocks in the plc software to path")]
        public static async Task<ResponseExportBlocksAsDocuments> ExportBlocksAsDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: defines the path where to export the documents")] string exportPath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportBlocksAsDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks as documents from '{softwarePath}' to '{exportPath}'");
                
                var allBlocks = await Task.Run(() => Portal.GetBlocks(softwarePath, regexName));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No blocks found to export as documents",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalBlocks,
                        Message = $"Starting export of {totalBlocks} blocks as documents...",
                        progressToken
                    });
                }

                // Export blocks as documents asynchronously
                var exportedBlocks = await Task.Run(() => Portal.ExportBlocksAsDocuments(softwarePath, exportPath, regexName, preservePath));
                
                // Send progress update after export completion
                if (exportedBlocks != null && progressToken != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalBlocks,
                        Message = $"Exported {exportedCount} of {totalBlocks} blocks as documents",
                        progressToken
                    });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);

                            responseList.Add(new ResponseBlockInfo
                            {
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalBlocks,
                            Message = $"Document export completed: {processedCount} blocks exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    Logger?.LogInformation($"Document export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"Document export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{exportPath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting documents to '{exportPath}'");
                throw new McpException($"Unexpected error exporting documents to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportFromDocuments"), Description("Import program block from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+)")]
        public static ResponseImportFromDocuments ImportFromDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the block should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("fileNameWithoutExtension: name of the block file without extension") ] string fileNameWithoutExtension,
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                var option = ParseImportDocumentOption(importOption);

                // Pre-check .s7res for missing en-US tags
                var warnings = new JsonArray();
                try
                {
                    var missingIds = GetResMissingEnUsIds(importPath, fileNameWithoutExtension);
                    if (missingIds != null && missingIds.Count > 0)
                    {
                        Logger?.LogWarning($".s7res for '{fileNameWithoutExtension}' missing en-US tags for {missingIds.Count} items: {string.Join(", ", missingIds)}");
                        warnings.Add(new JsonObject
                        {
                            ["name"] = fileNameWithoutExtension,
                            ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to evaluate .s7res warnings");
                }

                var ok = Portal.ImportFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, option);
                if (ok)
                {
                    return new ResponseImportFromDocuments
                    {
                        Message = $"Imported '{fileNameWithoutExtension}' from '{importPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["warnings"] = warnings
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing '{fileNameWithoutExtension}' from '{importPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing from documents: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportBlocksFromDocuments"), Description("Import program blocks from SIMATIC SD documents (.s7dcl/.s7res) into PLC software (V20+)")]
        public static async Task<ResponseImportBlocksFromDocuments> ImportBlocksFromDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the blocks should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("regexName: name or regular expression to select block files (empty for all)")] string regexName = "",
            [Description("importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)")] string importOption = "Override")
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;

            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportBlocksFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                // Determine total by scanning .s7dcl files matching regex
                int total = 0;
                var scanWarnings = new JsonArray();
                try
                {
                    if (Directory.Exists(importPath))
                    {
                        var rx = string.IsNullOrWhiteSpace(regexName) ? null : new Regex(regexName, RegexOptions.Compiled);
                        var files = Directory.GetFiles(importPath, "*.s7dcl", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(f);
                            if (rx != null && !rx.IsMatch(name))
                                continue;
                            total++;

                            try
                            {
                                var missingIds = GetResMissingEnUsIds(importPath, name);
                                if (missingIds != null && missingIds.Count > 0)
                                {
                                    scanWarnings.Add(new JsonObject
                                    {
                                        ["name"] = name,
                                        ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { /* ignore pre-scan errors */ }

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = total,
                        Message = total > 0 ? $"Starting import of {total} blocks from documents..." : "Scanning import directory...",
                        progressToken
                    });
                }

                var option = ParseImportDocumentOption(importOption);
                var imported = await Task.Run(() => Portal.ImportBlocksFromDocuments(softwarePath, groupPath, importPath, regexName, option));

                var responseList = new List<ResponseBlockInfo>();
                int processed = 0;
                if (imported != null)
                {
                    foreach (var block in imported)
                    {
                        if (block != null)
                        {
                            var attributes = Helper.GetAttributeList(block);
                            responseList.Add(new ResponseBlockInfo
                            {
                                Name = block.Name,
                                TypeName = block.GetType().Name,
                                Namespace = block.Namespace,
                                ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                                MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                                IsConsistent = block.IsConsistent,
                                HeaderName = block.HeaderName,
                                ModifiedDate = block.ModifiedDate,
                                IsKnowHowProtected = block.IsKnowHowProtected,
                                Attributes = attributes,
                                Description = block.ToString()
                            });
                        }
                        processed++;
                    }
                }

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = processed,
                        Total = total,
                        Message = $"Document import completed: {processed} blocks imported successfully",
                        progressToken
                    });
                }

                var duration = (DateTime.Now - startTime).TotalSeconds;
                Logger?.LogInformation($"Document import completed: {processed} blocks imported in {duration:F2} seconds");

                return new ResponseImportBlocksFromDocuments
                {
                    Message = $"Document import completed: {processed} blocks imported from '{importPath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["totalBlocks"] = total,
                        ["importedBlocks"] = processed,
                        ["duration"] = duration,
                        ["warnings"] = scanWarnings
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document import failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch { }
                }

                Logger?.LogError(ex, $"Failed importing documents from '{importPath}'");
                throw new McpException($"Unexpected error importing documents from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static ImportDocumentOptions ParseImportDocumentOption(string option)
        {
            if (string.IsNullOrWhiteSpace(option)) return ImportDocumentOptions.Override;

            var normalized = option.Trim();

            // Primary: accept exact enum names (case-insensitive)
            if (Enum.TryParse<ImportDocumentOptions>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            // Aliases and common misspellings
            switch (normalized.ToLowerInvariant())
            {
                case "override": return ImportDocumentOptions.Override;
                case "none": return ImportDocumentOptions.None;
                case "skipinactiveculture":
                case "skipinactivecultures":
                case "skipinactive":
                case "skipinactivecult":
                    return ImportDocumentOptions.SkipInactiveCultures;
                case "activeinactiveculture":
                case "activateinactivecultures":
                case "activeinactivecultures":
                case "activateinactive":
                    return ImportDocumentOptions.ActivateInactiveCultures;
                default:
                    throw new McpException($"Invalid importOption '{option}'. Allowed: None, Override, SkipInactiveCultures, ActivateInactiveCultures", McpErrorCode.InvalidParams);
            }
        }

        private static List<string> GetResMissingEnUsIds(string directory, string baseName)
        {
            var resPath = Path.Combine(directory, baseName + ".s7res");
            var missing = new List<string>();
            if (!File.Exists(resPath))
            {
                return missing;
            }
            var xdoc = XDocument.Load(resPath);
            XNamespace ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
            foreach (var comment in xdoc.Descendants(ns + "Comment"))
            {
                var hasEnUs = comment.Elements(ns + "MultiLanguageText")
                                     .Any(e => string.Equals((string?)e.Attribute("Lang"), "en-US", StringComparison.OrdinalIgnoreCase));
                if (!hasEnUs)
                {
                    var id = (string?)comment.Attribute("Id") ?? "";
                    missing.Add(id);
                }
            }
            return missing;
        }

        #endregion

        #region tag tables

        [McpServerTool(Name = "GetTagTables"), Description("Get a list of PLC tag tables from plc software")]
        public static ResponseTagTables GetTagTables(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the tag table. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetTagTables(softwarePath, regexName);

                var responseList = new List<ResponseTagTableInfo>();
                foreach (var table in list)
                {
                    if (table != null)
                    {
                        var attributes = Helper.GetAttributeList(table);

                        responseList.Add(new ResponseTagTableInfo
                        {
                            Name = table.Name,
                            TagCount = table.Tags.Count,
                            Attributes = attributes
        #region external sources

        [McpServerTool(Name = "GetExternalSources"), Description("Get a list of external sources from the plc software")]
        public static ResponseExternalSources GetExternalSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the external source. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetExternalSources(softwarePath, regexName);

                var responseList = new List<ResponseExternalSourceInfo>();
                foreach (var source in list)
                {
                    if (source != null)
                    {
                        var extension = Path.GetExtension(source.Name);
                        responseList.Add(new ResponseExternalSourceInfo
                        {
                            Name = source.Name,
                            Extension = extension
                        });
                    }
                }

                return new ResponseTagTables
                {
                    Message = $"Tag tables with regex '{regexName}' retrieved from '{softwarePath}'",
                return new ResponseExternalSources
                {
                    Message = $"External sources with regex '{regexName}' retrieved from '{softwarePath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                        ["success"] = true,
                        ["count"] = responseList.Count
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tag tables with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetTagTable"), Description("Get a single PLC tag table with all its tags")]
        public static ResponseTagTable GetTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTableName: the name of the tag table")] string tagTableName)
        {
            try
            {
                var table = Portal.GetTagTable(softwarePath, tagTableName);

                if (table != null)
                {
                    var tags = new List<ResponseTagInfo>();
                    foreach (PlcTag tag in table.Tags)
                    {
                        tags.Add(new ResponseTagInfo
                        {
                            Name = tag.Name,
                            DataTypeName = tag.DataTypeName,
                            LogicalAddress = tag.LogicalAddress,
                            Comment = tag.Comment?.Items?.Count > 0 ? tag.Comment.Items[0].Text : ""
                        });
                    }

                    return new ResponseTagTable
                    {
                        Message = $"Tag table '{tagTableName}' retrieved from '{softwarePath}'",
                        Name = table.Name,
                        Tags = tags,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["tagCount"] = tags.Count
        #region online access

        [McpServerTool(Name = "GoOnline"), Description("Establish an online connection to a PLC device")]
        public static ResponseOnlineStatus GoOnline(
            [Description("deviceItemPath: path to the device item (e.g., 'PLC_1/PLC_1')")] string deviceItemPath)
        {
            try
            {
                var result = Portal.GoOnline(deviceItemPath);

                return new ResponseOnlineStatus
                {
                    Message = result ? $"Successfully connected online to '{deviceItemPath}'" : $"Failed to go online for '{deviceItemPath}'",
                    IsOnline = result,
                    DevicePath = deviceItemPath,
                    Mode = "Online",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = result
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Device item not found: {deviceItemPath}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot go online: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed going online for '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error going online for '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GoOffline"), Description("Disconnect an online connection from a PLC device")]
        public static ResponseOnlineStatus GoOffline(
            [Description("deviceItemPath: path to the device item (e.g., 'PLC_1/PLC_1')")] string deviceItemPath)
        {
            try
            {
                var result = Portal.GoOffline(deviceItemPath);

                return new ResponseOnlineStatus
                {
                    Message = result ? $"Successfully disconnected from '{deviceItemPath}'" : $"Failed to go offline for '{deviceItemPath}'",
                    IsOnline = !result,
                    DevicePath = deviceItemPath,
                    Mode = "Offline",
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = result
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Device item not found: {deviceItemPath}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot go offline: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Failed going offline for '{deviceItemPath}': {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error going offline for '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DownloadToDevice"), Description("Download PLC program to a physical device")]
        public static ResponseDownloadResult DownloadToDevice(
            [Description("softwarePath: path in the project structure to the plc software")] string softwarePath,
            [Description("deviceItemPath: path to the device item (e.g., 'PLC_1/PLC_1')")] string deviceItemPath)
        {
            try
            {
                var result = Portal.DownloadToDevice(softwarePath, deviceItemPath);

                if (result != null)
                {
                    var warnings = new List<string>();
                    var errors = new List<string>();
                    bool success = result.State == DownloadResultState.Success || result.State == DownloadResultState.Warning;

                    foreach (var message in result.Messages)
                    {
                        if (message.State == DownloadResultMessageState.Warning)
                        {
                            warnings.Add(message.Message);
                        }
                        else if (message.State == DownloadResultMessageState.Error)
                        {
                            errors.Add(message.Message);
                        }
                    }

                    return new ResponseDownloadResult
                    {
                        Message = success ? $"Download to '{deviceItemPath}' completed successfully" : $"Download to '{deviceItemPath}' failed",
                        Success = success,
                        Warnings = warnings,
                        Errors = errors,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = success
                        }
                    };
                }

                throw new McpException($"Tag table '{tagTableName}' not found in '{softwarePath}'", McpErrorCode.InvalidParams);
                throw new McpException($"Unexpected error retrieving external sources with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportExternalSource"), Description("Import an external source file (.scl, .awl, etc.) into the plc software")]
        public static ResponseImportExternalSource ImportExternalSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group (currently unused, reserved for future sub-group support)")] string groupPath,
            [Description("importPath: defines the file path of the external source file to import (.scl, .awl, etc.)")] string importPath)
        {
            try
            {
                if (Portal.ImportExternalSource(softwarePath, groupPath, importPath))
                {
                    return new ResponseImportExternalSource
                    {
                        Message = $"External source imported from '{importPath}'",
                throw new McpException($"Failed downloading to device '{deviceItemPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Device or software not found: {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot download: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Download failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error downloading to '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "UploadFromDevice"), Description("Upload PLC program from a physical device to the project")]
        public static ResponseDownloadResult UploadFromDevice(
            [Description("softwarePath: path in the project structure to the plc software")] string softwarePath,
            [Description("deviceItemPath: path to the device item (e.g., 'PLC_1/PLC_1')")] string deviceItemPath)
        {
            try
            {
                var result = Portal.UploadFromDevice(softwarePath, deviceItemPath);

                if (result != null)
                {
                    var warnings = new List<string>();
                    var errors = new List<string>();
                    bool success = result.State == DownloadResultState.Success || result.State == DownloadResultState.Warning;

                    foreach (var message in result.Messages)
                    {
                        if (message.State == DownloadResultMessageState.Warning)
                        {
                            warnings.Add(message.Message);
                        }
                        else if (message.State == DownloadResultMessageState.Error)
                        {
                            errors.Add(message.Message);
                        }
                    }

                    return new ResponseDownloadResult
                    {
                        Message = success ? $"Upload from '{deviceItemPath}' completed successfully" : $"Upload from '{deviceItemPath}' failed",
                        Success = success,
                        Warnings = warnings,
                        Errors = errors,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = success
                        }
                    };
                }

                throw new McpException($"Failed uploading from device '{deviceItemPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Device or software not found: {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot upload: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Upload failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error uploading from '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region compare

        [McpServerTool(Name = "CompareOfflineOnline"), Description("Compare project software with online PLC to find differences")]
        public static ResponseCompareResult CompareOfflineOnline(
            [Description("softwarePath: path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var differences = Portal.CompareOfflineOnline(softwarePath);

                var items = differences.Select(d => new ComparisonDifference
                {
                    ObjectPath = d.ObjectPath,
                    ChangeType = d.ChangeType,
                    Details = d.Details
                }).ToList();

                return new ResponseCompareResult
                {
                    Message = items.Count > 0
                        ? $"Found {items.Count} difference(s) between offline and online for '{softwarePath}'"
                        : $"No differences found between offline and online for '{softwarePath}'",
                    Differences = items,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["differenceCount"] = items.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Software not found: {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot compare: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Compare failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error comparing offline/online for '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CompareBlocks"), Description("Compare two PLC blocks and find differences in their attributes")]
        public static ResponseCompareResult CompareBlocks(
            [Description("softwarePath: path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath1: path to the first block")] string blockPath1,
            [Description("blockPath2: path to the second block")] string blockPath2)
        {
            try
            {
                var differences = Portal.CompareBlocks(softwarePath, blockPath1, blockPath2);

                var items = differences.Select(d => new ComparisonDifference
                {
                    ObjectPath = d.Property,
                    ChangeType = "Modified",
                    Details = $"Block1='{d.Value1}' vs Block2='{d.Value2}'"
                }).ToList();

                return new ResponseCompareResult
                {
                    Message = items.Count > 0
                        ? $"Found {items.Count} difference(s) between '{blockPath1}' and '{blockPath2}'"
                        : $"No differences found between '{blockPath1}' and '{blockPath2}'",
                    Differences = items,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["differenceCount"] = items.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Block not found: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"Compare blocks failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error comparing blocks: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region library management

        [McpServerTool(Name = "GetProjectLibrary"), Description("Get project library contents including master copies and library types")]
        public static ResponseLibraryContents GetProjectLibrary(
            [Description("regexName: optional regex filter for library item names, default: no filter")] string regexName = "")
        {
            try
            {
                var (masterCopies, types) = Portal.GetProjectLibrary(regexName);

                var mcItems = masterCopies.Select(mc => new ResponseMasterCopy
                {
                    Name = mc.Name,
                    Path = mc.Path
                }).ToList();

                var typeItems = types.Select(t => new ResponseLibraryType
                {
                    Name = t.Name,
                    Version = t.Version
                }).ToList();

                return new ResponseLibraryContents
                {
                    Message = $"Project library: {mcItems.Count} master copies, {typeItems.Count} types",
                    MasterCopies = mcItems,
                    Types = typeItems,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["masterCopyCount"] = mcItems.Count,
                        ["typeCount"] = typeItems.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot access project library: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"GetProjectLibrary failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting project library: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetGlobalLibraries"), Description("List connected global libraries")]
        public static ResponseLibraries GetGlobalLibraries(
            [Description("regexName: optional regex filter for library names, default: no filter")] string regexName = "")
        {
            try
            {
                var libraries = Portal.GetGlobalLibraries(regexName);

                var items = libraries.Select(l => new ResponseLibrary
                {
                    Name = l.Name,
                    Path = l.Path
                }).ToList();

                return new ResponseLibraries
                {
                    Message = $"Found {items.Count} global libraries",
                    Items = items,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = items.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw new McpException($"GetGlobalLibraries failed: {pex.Message}", pex, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting global libraries: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "OpenGlobalLibrary"), Description("Open a global library from a file path")]
        public static ResponseOpenGlobalLibrary OpenGlobalLibrary(
            [Description("libraryPath: full file path to the global library file")] string libraryPath)
        {
            try
            {
                var result = Portal.OpenGlobalLibrary(libraryPath);

                if (result)
                {
                    return new ResponseOpenGlobalLibrary
                    {
                        Message = $"Global library '{libraryPath}' opened successfully",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing external source from '{importPath}'", McpErrorCode.InternalError);
                }

                throw new McpException($"Failed opening global library '{libraryPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing external source from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GenerateBlocksFromSource"), Description("Generate (compile) blocks from an external source in the plc software")]
        public static ResponseGenerateBlocksFromSource GenerateBlocksFromSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourceName: the name of the external source to generate blocks from")] string sourceName)
        {
            try
            {
                if (Portal.GenerateBlocksFromSource(softwarePath, sourceName))
                {
                    return new ResponseGenerateBlocksFromSource
                    {
                        Success = true,
                        SourceName = sourceName,
                        Message = $"Blocks generated from external source '{sourceName}'",
                        throw new McpException($"Library file not found: {libraryPath}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"OpenGlobalLibrary failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening global library '{libraryPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CopyToLibrary"), Description("Copy a block from the project to the project library as a master copy")]
        public static ResponseCopyToLibrary CopyToLibrary(
            [Description("softwarePath: path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: path to the block to copy")] string blockPath,
            [Description("libraryFolder: optional target folder in the library, default: root")] string libraryFolder = "")
        {
            try
            {
                var result = Portal.CopyToLibrary(softwarePath, blockPath, libraryFolder);

                if (result)
                {
                    return new ResponseCopyToLibrary
                    {
                        Message = $"Block '{blockPath}' copied to project library successfully",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed copying block '{blockPath}' to library", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Block not found: {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot copy to library: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"CopyToLibrary failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error copying to library: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CopyFromLibrary"), Description("Copy a master copy from the project library into the project")]
        public static ResponseCopyFromLibrary CopyFromLibrary(
            [Description("softwarePath: path in the project structure to the plc software")] string softwarePath,
            [Description("masterCopyName: name of the master copy in the project library")] string masterCopyName,
            [Description("targetGroupPath: target block group path in the software, default: root")] string targetGroupPath = "")
        {
            try
            {
                var result = Portal.CopyFromLibrary(softwarePath, masterCopyName, targetGroupPath);

                if (result)
                {
                    return new ResponseCopyFromLibrary
                    {
                        Message = $"Master copy '{masterCopyName}' copied to '{targetGroupPath}' successfully",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed copying master copy '{masterCopyName}' from library", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Not found: {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot copy from library: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"CopyFromLibrary failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error copying from library: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetLibraryTypes"), Description("Get library types from project or a named global library")]
        public static ResponseLibraryContents GetLibraryTypes(
            [Description("libraryName: name of the global library, default: project library")] string libraryName = "")
        {
            try
            {
                var types = Portal.GetLibraryTypes(libraryName);

                var typeItems = types.Select(t => new ResponseLibraryType
                {
                    Name = t.Name,
                    Version = t.Version
                }).ToList();

                var source = string.IsNullOrEmpty(libraryName) ? "project library" : $"global library '{libraryName}'";

                return new ResponseLibraryContents
                {
                    Message = $"Found {typeItems.Count} types in {source}",
                    Types = typeItems,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["typeCount"] = typeItems.Count
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException($"Library not found: {pex.Message}", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException($"Cannot access library: {pex.Message}", McpErrorCode.InvalidParams);
                    default:
                        throw new McpException($"GetLibraryTypes failed: {pex.Message}", pex, McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting library types: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region project creation

        [McpServerTool(Name = "CreateProject"), Description("Create a new empty TIA Portal project")]
        public static ResponseCreateProject CreateProject(
            [Description("projectPath: directory path where the project will be created")] string projectPath,
            [Description("projectName: name of the new project")] string projectName)
        {
            try
            {
                var result = Portal.CreateProject(projectPath, projectName);

                if (result)
                {
                    return new ResponseCreateProject
                    {
                        Message = $"Project '{projectName}' created successfully at '{projectPath}'",
                        Name = projectName,
                        Path = projectPath,
                        Success = true,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed generating blocks from external source '{sourceName}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null)
                            {
                                msg += $" Available: {string.Join(", ", pex.Candidates)}";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                            if (pex.Candidates != null && pex.Candidates.Any())
                            {
                                msg += $" Did you mean: {string.Join(", ", pex.Candidates)}?";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tag table '{tagTableName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetTags"), Description("Get tags from a PLC tag table, optionally filtered by name or regex")]
        public static ResponseTags GetTags(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTableName: the name of the tag table")] string tagTableName,
            [Description("regexName: defines the name or regular expression to find the tag. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetTags(softwarePath, tagTableName, regexName);

                var responseList = new List<ResponseTagInfo>();
                foreach (var tag in list)
                {
                    if (tag != null)
                    {
                        responseList.Add(new ResponseTagInfo
                        {
                            Name = tag.Name,
                            DataTypeName = tag.DataTypeName,
                            LogicalAddress = tag.LogicalAddress,
                            Comment = tag.Comment?.Items?.Count > 0 ? tag.Comment.Items[0].Text : ""
                        });
                    }
                }

                return new ResponseTags
                {
                    Message = $"Tags with regex '{regexName}' retrieved from table '{tagTableName}' in '{softwarePath}'",
                throw new McpException($"Unexpected error generating blocks from external source '{sourceName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteExternalSource"), Description("Delete an external source from the plc software")]
        public static ResponseDeleteExternalSource DeleteExternalSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourceName: the name of the external source to delete")] string sourceName)
        {
            try
            {
                if (Portal.DeleteExternalSource(softwarePath, sourceName))
                {
                    return new ResponseDeleteExternalSource
                    {
                        Message = $"External source '{sourceName}' deleted",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed deleting external source '{sourceName}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null && pex.Candidates.Any())
                            {
                                msg += $" Did you mean: {string.Join(", ", pex.Candidates)}?";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting external source '{sourceName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportExternalSource"), Description("Export an external source from the plc software to a file")]
        public static ResponseExportExternalSource ExportExternalSource(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourceName: the name of the external source to export")] string sourceName,
            [Description("exportPath: defines the directory path where to export the external source")] string exportPath)
        {
            try
            {
                if (Portal.ExportExternalSource(softwarePath, sourceName, exportPath))
                {
                    return new ResponseExportExternalSource
                    {
                        Message = $"External source '{sourceName}' exported to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting external source '{sourceName}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null && pex.Candidates.Any())
                            {
                                msg += $" Did you mean: {string.Join(", ", pex.Candidates)}?";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }
                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export external source.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportExternalSource failed for {SoftwarePath} {SourceName} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["sourceName"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting external source '{sourceName}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);

                throw new McpException($"Failed creating project '{projectName}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw new McpException($"CreateProject failed: {pex.Message}", pex, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating project '{projectName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region cross-references

        [McpServerTool(Name = "GetCrossReferences"), Description("Get cross-references for a block or type in the plc software")]
        public static ResponseCrossReferences GetCrossReferences(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("objectPath: full path to the block or type in the project structure, e.g. 'Group/Subgroup/Name'")] string objectPath)
        {
            try
            {
                var list = Portal.GetCrossReferences(softwarePath, objectPath);

                var responseList = new List<ResponseCrossReferenceInfo>();
                foreach (var (sourceObject, referencedObject, referenceType, path) in list)
                {
                    responseList.Add(new ResponseCrossReferenceInfo
                    {
                        SourceObject = sourceObject,
                        ReferencedObject = referencedObject,
                        ReferenceType = referenceType,
                        Path = path
                    });
                }

                return new ResponseCrossReferences
                {
                    Message = $"Cross-references retrieved for '{objectPath}' in '{softwarePath}'",
                    Items = responseList,
        #region multi-user

        [McpServerTool(Name = "GetMultiuserInfo"), Description("Get multi-user session information for the current project")]
        public static ResponseMultiuserInfo GetMultiuserInfo()
        {
            try
            {
                var (isMultiuser, serverName, users) = Portal.GetMultiuserInfo();

                return new ResponseMultiuserInfo
                {
                    Message = isMultiuser
                        ? $"Multi-user session active on server '{serverName}' with {users.Count} user(s)"
                        : "Project is not a multi-user session",
                    IsMultiuser = isMultiuser,
                    ServerName = serverName,
                    Users = users,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true,
                        ["count"] = responseList.Count
                        ["isMultiuser"] = isMultiuser
                    }
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null)
                            {
                                msg += $" Available: {string.Join(", ", pex.Candidates)}";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving tags from table '{tagTableName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportTagTable"), Description("Export a PLC tag table from plc software to XML file")]
        public static ResponseExportTagTable ExportTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTableName: the name of the tag table to export")] string tagTableName,
            [Description("exportPath: defines the directory path where to export the tag table")] string exportPath)
        {
            try
            {
                var table = Portal.ExportTagTable(softwarePath, tagTableName, exportPath);
                if (table != null)
                {
                    return new ResponseExportTagTable
                    {
                        Message = $"Tag table '{tagTableName}' exported to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed exporting tag table '{tagTableName}' to '{exportPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null)
                            {
                                msg += $" Available: {string.Join(", ", pex.Candidates)}";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export tag table.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportTagTable failed for {SoftwarePath} {TagTableName} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["tagTableName"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting tag table '{tagTableName}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportTagTable"), Description("Import a PLC tag table from XML file to plc software")]
        public static ResponseImportTagTable ImportTagTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: defines the path of the xml file from where to import the tag table")] string importPath)
        {
            try
            {
                if (Portal.ImportTagTable(softwarePath, importPath))
                {
                    return new ResponseImportTagTable
                    {
                        Message = $"Tag table imported from '{importPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing tag table from '{importPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing tag table from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CreateTag"), Description("Create a new PLC tag in a tag table")]
        public static ResponseCreateTag CreateTag(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTableName: the name of the tag table where to create the tag")] string tagTableName,
            [Description("tagName: the name of the new tag")] string tagName,
            [Description("dataType: the data type of the tag, e.g. 'Bool', 'Int', 'Real'")] string dataType,
            [Description("logicalAddress: the logical address of the tag, e.g. '%I0.0', '%Q0.0', '%M0.0'")] string logicalAddress,
            [Description("comment: optional comment for the tag")] string comment = "")
        {
            try
            {
                var tag = Portal.CreateTag(softwarePath, tagTableName, tagName, dataType, logicalAddress, comment);
                if (tag != null)
                {
                    return new ResponseCreateTag
                    {
                        Message = $"Tag '{tagName}' created in table '{tagTableName}'",
                        Tag = new ResponseTagInfo
                        {
                            Name = tag.Name,
                            DataTypeName = tag.DataTypeName,
                            LogicalAddress = tag.LogicalAddress,
                            Comment = comment
                        },
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed creating tag '{tagName}' in table '{tagTableName}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null)
                            {
                                msg += $" Available: {string.Join(", ", pex.Candidates)}";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error creating tag '{tagName}' in table '{tagTableName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "DeleteTag"), Description("Delete a PLC tag from a tag table")]
        public static ResponseDeleteTag DeleteTag(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("tagTableName: the name of the tag table containing the tag")] string tagTableName,
            [Description("tagName: the name of the tag to delete")] string tagName)
        {
            try
            {
                if (Portal.DeleteTag(softwarePath, tagTableName, tagName))
                {
                    return new ResponseDeleteTag
                    {
                        Message = $"Tag '{tagName}' deleted from table '{tagTableName}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed deleting tag '{tagName}' from table '{tagTableName}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null)
                            {
                                msg += $" Available: {string.Join(", ", pex.Candidates)}";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error deleting tag '{tagName}' from table '{tagTableName}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region watch/force tables

        [McpServerTool(Name = "GetWatchTables"), Description("Get a list of watch and force tables from plc software")]
        public static ResponseWatchTables GetWatchTables(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the watch table. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetWatchTables(softwarePath, regexName);

                var responseList = new List<ResponseWatchTableInfo>();
                foreach (var table in list)
                {
                    if (table != null)
                    {
                        var attributes = Helper.GetAttributeList(table);

                        responseList.Add(new ResponseWatchTableInfo
                        {
                            Name = table.Name,
                            Attributes = attributes
                        });
                    }
                }

                return new ResponseWatchTables
                {
                    Message = $"Watch tables with regex '{regexName}' retrieved from '{softwarePath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = true
                    }
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving watch tables with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportWatchTable"), Description("Export a watch table from plc software to XML file")]
        public static ResponseExportWatchTable ExportWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("watchTableName: the name of the watch table to export")] string watchTableName,
            [Description("exportPath: defines the directory path where to export the watch table")] string exportPath)
        {
            try
            {
                var table = Portal.ExportWatchTable(softwarePath, watchTableName, exportPath);
                if (table != null)
                {
                    return new ResponseExportWatchTable
                    {
                        Message = $"Watch table '{watchTableName}' exported to '{exportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }

                throw new McpException($"Failed exporting watch table '{watchTableName}' to '{exportPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var msg = pex.Message;
                            if (pex.Candidates != null)
                            {
                                msg += $" Available: {string.Join(", ", pex.Candidates)}";
                            }
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export watch table.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportWatchTable failed for {SoftwarePath} {WatchTableName} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["watchTableName"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting watch table '{watchTableName}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportWatchTable"), Description("Import a watch table from XML file to plc software")]
        public static ResponseImportWatchTable ImportWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: defines the path of the xml file from where to import the watch table")] string importPath)
        {
            try
            {
                if (Portal.ImportWatchTable(softwarePath, importPath))
                {
                    return new ResponseImportWatchTable
                    {
                        Message = $"Watch table imported from '{importPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing watch table from '{importPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                }

                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing watch table from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
                throw new McpException($"Unexpected error retrieving cross-references for '{objectPath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
                throw new McpException($"GetMultiuserInfo failed: {pex.Message}", pex, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error getting multi-user info: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion
    }
}

