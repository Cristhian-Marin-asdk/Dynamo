﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
//using System.Windows.Input;
using Dynamo.Core;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Dynamo.GraphNodeManager.ViewModels;
using Dynamo.Models;
using Dynamo.Wpf.Extensions;
using Dynamo.Extensions;
using Dynamo.PackageManager;
using Dynamo.Utilities;
using Dynamo.ViewModels;
using Microsoft.Practices.Prism.Commands;
using Newtonsoft.Json;
using DelegateCommand = Dynamo.UI.Commands.DelegateCommand;
using NodeViewModel = Dynamo.GraphNodeManager.ViewModels.NodeViewModel;

namespace Dynamo.GraphNodeManager
{
    /// <summary>
    /// ViewModel for the Graph Node Manager
    /// Handles node table setup, workspace events, etc.
    /// Source: TuneUp https://github.com/DynamoDS/TuneUp/blob/master/TuneUp/TuneUpWindowViewModel.cs
    /// </summary>
    public class GraphNodeManagerViewModel : NotificationObject, IDisposable
    {
        #region Private Properties
        private readonly ViewLoadedParams viewLoadedParams;

        private HomeWorkspaceModel currentWorkspace;

        private readonly ViewModelCommandExecutive viewModelCommandExecutive;

        private readonly string uniqueId;

        private readonly Dictionary<Guid, NodeViewModel> nodeDictionary = new Dictionary<Guid, NodeViewModel>();
        private Dictionary<string, FilterViewModel> filterDictionary = new Dictionary<string, FilterViewModel>();

        private bool isEditingEnabled = true;
        private bool isAnyFilterOn = false;

        private HomeWorkspaceModel CurrentWorkspace
        {
            get
            {
                return currentWorkspace;
            }
            set
            {
                // Unsubscribe from old workspace
                if (currentWorkspace != null)
                {
                    UnsubscribeWorkspaceEvents(currentWorkspace);
                }

                // Subscribe to new workspace
                if (value != null)
                {
                    // Set new workspace
                    currentWorkspace = value;
                    SubscribeWorkspaceEvents(currentWorkspace);
                }
            }
        }

        private ViewModelCommandExecutive viewModelExecutive;
        private readonly ICommandExecutive commandExecutive;

        #endregion

        #region Public Properties
        /// <summary>
        /// Collection of data for nodes in the current workspace
        /// </summary>
        public ObservableCollection<NodeViewModel> Nodes { get; set; } = new ObservableCollection<NodeViewModel>();

        /// <summary>
        /// Collection of user filters
        /// </summary>
        public ObservableCollection<FilterViewModel> FilterItems { get; set; } = new ObservableCollection<FilterViewModel>();

        /// <summary>
        /// Collection of all current Workspace Nodes
        /// </summary>
        public CollectionViewSource NodesCollection { get; set; }

        public DelegateCommand NodeSelectCommand { get; set; }
        public DelegateCommand ClearFiltersCommand { get; set; }
        public DelegateCommand<string> ExportCommand { get; set; }


        public string searchText;
        /// <summary>
        /// Search Box Text binding
        /// </summary>
        [JsonIgnore]
        public string SearchText
        {
            get { return searchText; }
            set
            {
                searchText = value;

                // Every time we type in the Search Box, we will be updating the Filter
                NodesCollectionFilter_Changed();    
                RaisePropertyChanged(nameof(SearchText));
            }
        }

        public GraphNodeManagerView GraphNodeManagerView;

        public string searchBoxPrompt = "Search..";
        /// <summary>
        /// Search Box Prompt binding
        /// </summary>
        [JsonIgnore]
        public string SearchBoxPrompt
        {
            get { return searchBoxPrompt; }
            set
            {
                searchBoxPrompt = value;
                RaisePropertyChanged(nameof(SearchBoxPrompt));
            }
        }

        private bool isRecomputeEnabled = true;
        /// <summary>
        /// Is the recomputeAll button enabled in the UI. Users should not be able to force a 
        /// reset of the engine and re-execution of the graph if one is still ongoing. This causes...trouble.
        /// Source: TuneUp https://github.com/DynamoDS/TuneUp
        /// </summary>
        [JsonIgnore]
        public bool IsRecomputeEnabled
        {
            get => isRecomputeEnabled;
            private set
            {
                if (isRecomputeEnabled != value)
                {
                    isRecomputeEnabled = value;
                    RaisePropertyChanged(nameof(IsRecomputeEnabled));
                }
            }
        }

        internal string DynamoVersion;
        internal string HostName;

        [JsonIgnore]
        public bool IsAnyFilterOn
        {
            get
            {
                return FilterItems.Any(f => f.IsFilterOn);
            }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// Initialize the ViewLoadedParameters that contain most of the interesting stuff 
        /// Subscribe to the Workspace Changed and Cleared Events
        /// Establish the Current Workspace
        /// </summary>
        /// <param name="p"></param>
        public GraphNodeManagerViewModel(ViewLoadedParams p, string id)
        {
            this.viewLoadedParams = p;

            p.CurrentWorkspaceChanged += OnCurrentWorkspaceChanged;
            p.CurrentWorkspaceCleared += OnCurrentWorkspaceCleared;
            
            if (p.CurrentWorkspaceModel is HomeWorkspaceModel)
            {
                CurrentWorkspace = p.CurrentWorkspaceModel as HomeWorkspaceModel;
                viewModelExecutive = p.ViewModelCommandExecutive;
                commandExecutive = p.CommandExecutive;
                viewModelCommandExecutive = p.ViewModelCommandExecutive;
                uniqueId = id;
            }

            InitializeFilters();

            NodeSelectCommand = new DelegateCommand(NodeSelect);
            ClearFiltersCommand = new DelegateCommand(ClearAllFilters);
            ExportCommand = new DelegateCommand<string>(ExportGraph);

            DynamoVersion = p.StartupParams.DynamoVersion.ToString();

            var dynamoViewModel = p.DynamoWindow.DataContext as DynamoViewModel;
            HostName = dynamoViewModel.Model.HostName;  // will become obsolete in Dynamo 3.0

            // For node package info
            var pmExtension = viewLoadedParams.ViewStartupParams.ExtensionManager.Extensions.OfType<PackageManagerExtension>().FirstOrDefault();

        }


        private void InitializeFilters()
        {
            FilterItems.Add(new FilterViewModel(this){Name = "Empty list"});
            FilterItems.Add(new FilterViewModel(this){Name = "Error"});
            FilterItems.Add(new FilterViewModel(this){Name = "Frozen"});
            FilterItems.Add(new FilterViewModel(this){Name = "Missing content"});
            FilterItems.Add(new FilterViewModel(this){Name = "Function"});
            FilterItems.Add(new FilterViewModel(this){Name = "Information"});
            FilterItems.Add(new FilterViewModel(this){Name = "Is Input"});
            FilterItems.Add(new FilterViewModel(this){Name = "Is Output"});
            FilterItems.Add(new FilterViewModel(this){Name = "Null"});
            FilterItems.Add(new FilterViewModel(this){Name = "Warning"});
            FilterItems.Add(new FilterViewModel(this){Name = "Preview off"});

            filterDictionary = new Dictionary<string, FilterViewModel>(FilterItems.ToDictionary(fi => fi.Name));
        }

        #endregion

        #region Node Methods
        /// <summary>
        /// The main method to set the Node Collection
        /// Will be triggered in two occasions:
        /// - a new workspace is established
        /// - on Run (or after Run has finished?)
        /// </summary>
        private void ResetNodes()
        {
            if (CurrentWorkspace == null)
            {
                return;
            }
            nodeDictionary.Clear();
            Nodes.Clear();
            foreach (var node in CurrentWorkspace.Nodes)
            {
                var graphNode = new NodeViewModel(node);
                nodeDictionary[node.GUID] = graphNode;
                Nodes.Add(graphNode);
            }

            NodesCollection = new CollectionViewSource();
            NodesCollection.Source = Nodes;
            NodesCollection.Filter += NodesCollectionViewSource_Filter;

            NodesCollection.View?.Refresh();

            RaisePropertyChanged(nameof(NodesCollection));
            RaisePropertyChanged(nameof(Nodes));
        }
        
        /// <summary>
        /// Enable editing when it is disabled temporarily.
        /// </summary>
        internal void EnableEditing()
        {
            if (!isEditingEnabled && CurrentWorkspace != null)
            {
                ResetNodes();
                CurrentWorkspace.EngineController.EnableProfiling(true, CurrentWorkspace, CurrentWorkspace.Nodes);
                isEditingEnabled = true;
            }
            RaisePropertyChanged(nameof(NodesCollection));
        }

        /// <summary>
        /// Zoom around the currently selected Node
        /// </summary>
        /// <param name="obj"></param>
        internal void NodeSelect(object obj)
        {
            var nodeViewModel = obj as NodeViewModel;
            if (nodeViewModel == null) return;

            // Select
            var command = new DynamoModel.SelectModelCommand(nodeViewModel.NodeModel.GUID, ModifierKeys.None);  
            commandExecutive.ExecuteCommand(command, uniqueId, "GraphNodeManager");

            // Focus on selected
            viewModelCommandExecutive.FindByIdCommand(nodeViewModel.NodeModel.GUID.ToString());
        }

        /// <summary>
        /// Switches off all filters
        /// </summary>
        /// <param name="obj"></param>
        internal void ClearAllFilters(object obj)
        {
            if (!FilterItems.Any()) return;

            foreach (FilterViewModel fvm in FilterItems)
            {
                fvm.IsFilterOn = false;
            }
            // Refresh the view 
            NodesCollectionFilter_Changed();
        }

        /// <summary>
        /// Export the current graph to CSV or JSON
        /// </summary>
        /// <param name="parameter"></param>
        /// <exception cref="NotImplementedException"></exception>
        internal void ExportGraph(object parameter)
        {
            if (parameter == null) return;
            string type = parameter.ToString();

            switch (type)
            {
                case "CSV":
                    Utilities.Utilities.ExportToCSV(Nodes.ToArray());
                    break;
                case "JSON":
                    Utilities.Utilities.ExportToJson(Nodes.ToArray());
                    break;
            }
        }

        /// <summary>
        /// On changing a condition that affects the filter
        /// </summary>
        internal void NodesCollectionFilter_Changed()
        {
            // Refresh the view to apply filters.
            RaisePropertyChanged(nameof(IsAnyFilterOn));
            CollectionViewSource.GetDefaultView(GraphNodeManagerView.NodesInfoDataGrid.ItemsSource).Refresh();
        }

        /// <summary>
        /// Applies filter based on the Search Bar
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NodesCollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (!(e.Item is NodeViewModel nvm)) return;
            if (!filterDictionary.Any()) return;

            // Boolean Toggle Filters
            if (!nvm.IsEmptyList && filterDictionary["Empty list"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.IssuesHasError && filterDictionary["Error"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.StatusIsFrozen && filterDictionary["Frozen"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.IsDummyNode && filterDictionary["Missing content"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.StateIsFunction && filterDictionary["Function"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.IsInfo && filterDictionary["Information"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.StateIsInput && filterDictionary["Is Input"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.StateIsOutput && filterDictionary["Is Output"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.IsNull && filterDictionary["Null"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.IssuesHasWarning && filterDictionary["Warning"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }
            if (!nvm.StatusIsHidden && filterDictionary["Preview off"].IsFilterOn)
            {
                e.Accepted = false;
                return;
            }

            // Textual SearchBox Filter
            if (string.IsNullOrEmpty(SearchText)) return;
            if (nvm.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                e.Accepted = true;
            else
            {
                e.Accepted = false;
                return;
            }

            e.Accepted = true;
        }
        #endregion

        #region Workspace EventHandlers

        /// <summary>
        /// On evaluation started lock  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentWorkspaceModel_EvaluationStarted(object sender, EventArgs e)
        {
            IsRecomputeEnabled = false;

            EnableEditing();
        }

        /// <summary>
        /// On evaluation finished unlock 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentWorkspaceModel_EvaluationCompleted(object sender, EventArgs e)
        {
            IsRecomputeEnabled = true;

            RaisePropertyChanged(nameof(Nodes));
            RaisePropertyChanged(nameof(NodesCollection));

            try
            {
                NodesCollection.Dispatcher.Invoke(() =>
                {
                    NodesCollection.SortDescriptions.Clear();

                    if (NodesCollection.View != null)
                        NodesCollection.View.Refresh();
                });
            }
            catch (InvalidOperationException invalidOperationException)
            {
                return;
            }
            catch (Exception exception)
            {
                return;
            }
        }

        /// <summary>
        /// When a new node is added, update the collection
        /// </summary>
        /// <param name="node"></param>
        private void CurrentWorkspaceModel_NodeAdded(NodeModel node)
        {
            var infoNode = new NodeViewModel(node);
            nodeDictionary[node.GUID] = infoNode;
            Nodes.Add(infoNode);
            RaisePropertyChanged(nameof(NodesCollection));
        }
        /// <summary>
        /// When a node is removed, update the collection
        /// </summary>
        /// <param name="node"></param>
        private void CurrentWorkspaceModel_NodeRemoved(NodeModel node)
        {
            var infoNode = nodeDictionary[node.GUID];
            nodeDictionary.Remove(node.GUID);
            Nodes.Remove(infoNode);
            RaisePropertyChanged(nameof(NodesCollection));
        }

        private void OnCurrentWorkspaceCleared(IWorkspaceModel workspace)
        {
            // Editing needs to be enabled per workspace so mark it false after closing
            isEditingEnabled = false;
            CurrentWorkspace = viewLoadedParams.CurrentWorkspaceModel as HomeWorkspaceModel;
        }

        private void OnCurrentWorkspaceChanged(IWorkspaceModel workspace)
        {
            // Editing needs to be enabled per workspace so mark it false after switching
            isEditingEnabled = false;
            CurrentWorkspace = workspace as HomeWorkspaceModel;
        }

        #endregion

        #region Setup and Dispose Methods

        /// <summary>
        /// When a new workspace is established
        /// </summary>
        /// <param name="workspace"></param>
        private void SubscribeWorkspaceEvents(HomeWorkspaceModel workspace)
        {
            workspace.NodeAdded += CurrentWorkspaceModel_NodeAdded;
            workspace.NodeRemoved += CurrentWorkspaceModel_NodeRemoved;
            workspace.EvaluationStarted += CurrentWorkspaceModel_EvaluationStarted;
            workspace.EvaluationCompleted += CurrentWorkspaceModel_EvaluationCompleted;

            ResetNodes();
        }

        /// <summary>
        /// When we change workspace
        /// </summary>
        /// <param name="workspace"></param>
        private void UnsubscribeWorkspaceEvents(HomeWorkspaceModel workspace)
        {
            workspace.NodeAdded -= CurrentWorkspaceModel_NodeAdded;
            workspace.NodeRemoved -= CurrentWorkspaceModel_NodeRemoved;
            workspace.EvaluationStarted -= CurrentWorkspaceModel_EvaluationStarted;
            workspace.EvaluationCompleted -= CurrentWorkspaceModel_EvaluationCompleted;
        }

        /// <summary>
        /// ViewModel dispose method
        /// </summary>
        public void Dispose()
        {
            UnsubscribeWorkspaceEvents(CurrentWorkspace);
            viewLoadedParams.CurrentWorkspaceChanged -= OnCurrentWorkspaceChanged;
            viewLoadedParams.CurrentWorkspaceCleared -= OnCurrentWorkspaceCleared;
            NodesCollection.Filter -= NodesCollectionViewSource_Filter;
            GraphNodeManagerView = null;
        }

        #endregion
    }
}