﻿using System.Linq;
using Dynamo.PackageManager;
using Dynamo.ViewModels;
using Dynamo.Wpf.Extensions;

namespace Dynamo.PackageDetails
{
    public class PackageDetailsViewExtension : ViewExtensionBase
    {
        internal PackageManagerExtension PackageManagerExtension { get; set; }
        internal PackageDetailsView PackageDetailsView { get; set; }
        internal PackageDetailsViewModel PackageDetailsViewModel { get; set; }
        internal PackageManagerClientViewModel PackageManagerClientViewModel { get; set; }
        internal ViewLoadedParams ViewLoadedParamsReference { get; set; }
        
        public override string UniqueId => "C71CA1B9-BF9F-425A-A12C-53DF56770406";

        public override string Name => Properties.Resources.PackageDetailsViewExtensionName;

        public override void Startup(ViewStartupParams viewStartupParams)
        {
            var packageManager = viewStartupParams.ExtensionManager.Extensions.OfType<PackageManagerExtension>().FirstOrDefault();
            PackageManagerExtension = packageManager;
        }

        public override void Loaded(ViewLoadedParams viewLoadedParams)
        {
            ViewLoadedParamsReference = viewLoadedParams;
            viewLoadedParams.ViewExtensionOpenRequestWithParameter += OnViewExtensionOpenWithParameterRequest;
            
            DynamoViewModel dynamoViewModel = viewLoadedParams.DynamoWindow.DataContext as DynamoViewModel;
            PackageManagerClientViewModel = dynamoViewModel.PackageManagerClientViewModel;
        }

        internal void OnViewExtensionOpenWithParameterRequest(string extensionName, object obj)
        {
            if (extensionName != Name || !(obj is PackageManagerSearchElement pmSearchElement)) return;
            OpenPackageDetails(pmSearchElement);
        }
        
        internal void OpenPackageDetails(PackageManagerSearchElement packageManagerSearchElement)
        {
            PackageDetailsViewModel = new PackageDetailsViewModel(this, packageManagerSearchElement);
            
            if (PackageDetailsView == null) PackageDetailsView = new PackageDetailsView();
            PackageDetailsView.DataContext = PackageDetailsViewModel;

            ViewLoadedParamsReference?.AddToExtensionsSideBar(this, PackageDetailsView);
        }
        
        public override void Dispose()
        {
            ViewLoadedParamsReference.ViewExtensionOpenRequestWithParameter -= OnViewExtensionOpenWithParameterRequest;
        }

        public override void Closed()
        {
            PackageDetailsViewModel = null;
            PackageDetailsView = null;
        }
    }
}