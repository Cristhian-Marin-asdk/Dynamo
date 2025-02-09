using Dynamo.ViewModels;
#if NETFRAMEWORK
using NotificationObject = Microsoft.Practices.Prism.ViewModel.NotificationObject;
#else
using NotificationObject = Dynamo.Core.NotificationObject;
#endif
using System.Collections.ObjectModel;

namespace Dynamo.PackageManager
{
    public class PackageManagerViewModel : NotificationObject
    {
        private DynamoViewModel dynamoViewModel;
        private InstalledPackagesViewModel installedPackagesViewModel;

        /// <summary>
        /// PreferenceViewModel containing the PackageManager paths and installed packages
        /// </summary>
        public PreferencesViewModel PreferencesViewModel { get; set; }

        /// <summary>
        /// PackageManagerSearchViewModel functionality to load Dynamo packages
        /// </summary>
        public PackageManagerSearchViewModel PackageSearchViewModel { get; set; }

        /// <summary>
        /// PublishPackageViewModel containing information about all the published packages
        /// </summary>
        public PublishPackageViewModel PublishPackageViewModel { get; set; }

        /// <summary>
        /// Returns all installed packages
        /// </summary>
        public ObservableCollection<PackageViewModel> LocalPackages => installedPackagesViewModel.LocalPackages;

        /// <summary>
        /// Returns all available filters
        /// </summary>
        public ObservableCollection<PackageFilter> Filters => installedPackagesViewModel.Filters;

        public PackageManagerViewModel(DynamoViewModel dynamoViewModel, PackageManagerSearchViewModel PkgSearchVM)
        {
            this.dynamoViewModel = dynamoViewModel;            
            this.PackageSearchViewModel = PkgSearchVM;            

            this.PreferencesViewModel = dynamoViewModel.PreferencesViewModel;
            var pathmanager = PreferencesViewModel.PackagePathsViewModel;

            if (PublishPackageViewModel == null)
            {
                PublishPackageViewModel = new PublishPackageViewModel(dynamoViewModel);
            }

            InitializeInstalledPackages();

            PkgSearchVM.RegisterTransientHandlers();
        }

        private void InitializeInstalledPackages()
        {
            if (this.dynamoViewModel.PackageManagerClientViewModel != null)
            {
                installedPackagesViewModel = new InstalledPackagesViewModel(dynamoViewModel, dynamoViewModel.PackageManagerClientViewModel.PackageManagerExtension.PackageLoader);
                installedPackagesViewModel?.PopulateFilters();
            }
        }

    }
}
