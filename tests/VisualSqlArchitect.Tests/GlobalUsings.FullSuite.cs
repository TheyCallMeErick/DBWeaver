global using System;
global using System.IO;
global using System.Linq;
global using System.Data;
global using System.Text;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using System.Reflection;
global using System.ComponentModel;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.Threading;
global using System.Threading.Tasks;

global using Xunit;

global using Avalonia;
global using Avalonia.Controls;
global using Avalonia.Input;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using DBWeaver.Core;
global using DBWeaver.Ddl;
global using DBWeaver.Metadata;
global using DBWeaver.Nodes;
global using DBWeaver.Nodes.Pins;
global using DBWeaver.Providers;
global using DBWeaver.QueryEngine;
global using DBWeaver.Registry;
global using DBWeaver.Compilation;
global using DBWeaver.CanvasKit;
global using DBWeaver.UI;
global using DBWeaver.UI.Services;
global using DBWeaver.UI.Services.Localization;
global using DBWeaver.UI.Services.CommandPalette;
global using DBWeaver.UI.Services.Connection;
global using DBWeaver.UI.Services.ConnectionManager;
global using DBWeaver.UI.Services.ConnectionManager.Models;
global using DBWeaver.UI.Services.QueryPreview;
global using DBWeaver.UI.Services.Validation;
global using DBWeaver.UI.Serialization;
global using DBWeaver.UI.ViewModels;
global using DBWeaver.UI.ViewModels.Canvas;
global using DBWeaver.UI.ViewModels.Canvas.Strategies;
global using DBWeaver.UI.ViewModels.UndoRedo;
global using DBWeaver.UI.ViewModels.UndoRedo.Commands;

global using DataType = DBWeaver.Nodes.PinDataType;
global using EDiagnosticStatus = DBWeaver.UI.Services.AppDiagnostics.Models.DiagnosticStatus;
global using EGuardSeverity = DBWeaver.UI.Services.Validation.GuardSeverity;
global using EIssueSeverity = DBWeaver.UI.Services.Validation.IssueSeverity;
global using EPreviewExecutionState = DBWeaver.UI.ViewModels.Canvas.PreviewExecutionState;
global using EConnectionActivationOutcome = DBWeaver.UI.Services.ConnectionManager.ConnectionActivationOutcome;
global using EConnectionHealthStatus = DBWeaver.UI.ViewModels.ConnectionHealthStatus;
