Name:           legion-toolkit
Version:        1.0.0
Release:        1%{?dist}
Summary:        Comprehensive control utility for Lenovo Legion laptops

License:        MIT
URL:            https://github.com/LenovoLegion/LegionToolkit
Source0:        %{name}-%{version}.tar.gz

BuildRequires:  dotnet-sdk-8.0
BuildRequires:  libX11-devel
BuildRequires:  libXrandr-devel
BuildRequires:  libXi-devel
BuildRequires:  systemd-rpm-macros

Requires:       dotnet-runtime-8.0
Requires:       libX11
Requires:       libXrandr
Requires:       libXi
Requires:       libnotify
Requires:       xorg-x11-utils
Requires:       ddcutil
Requires:       redshift

Recommends:     akmod-legion-laptop

%description
Legion Toolkit provides a graphical and command-line interface for
controlling various hardware features of Lenovo Legion laptops on Linux.

Features:
- Power mode control (Quiet, Balanced, Performance)
- Battery management (charge limits, conservation mode)
- Thermal monitoring and fan control
- RGB keyboard lighting control
- Display refresh rate and color management
- Automation profiles and rules
- System tray integration
- Comprehensive CLI support

This package requires the legion-laptop kernel module for full functionality.

%prep
%setup -q

%build
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

dotnet restore
dotnet publish LenovoLegionToolkit.Avalonia/LenovoLegionToolkit.Avalonia.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o %{buildroot}/opt/legion-toolkit

%install
rm -rf %{buildroot}

# Create directories
mkdir -p %{buildroot}/opt/legion-toolkit
mkdir -p %{buildroot}%{_bindir}
mkdir -p %{buildroot}%{_datadir}/applications
mkdir -p %{buildroot}%{_datadir}/icons/hicolor/scalable/apps
mkdir -p %{buildroot}%{_mandir}/man1
mkdir -p %{buildroot}%{_unitdir}
mkdir -p %{buildroot}%{_sysconfdir}/legion-toolkit

# Copy built files
cp -r bin/Release/linux-x64/* %{buildroot}/opt/legion-toolkit/

# Install binary symlink
ln -sf /opt/legion-toolkit/LenovoLegionToolkit.Avalonia %{buildroot}%{_bindir}/legion-toolkit

# Install desktop file
install -m 644 Resources/legion-toolkit.desktop %{buildroot}%{_datadir}/applications/

# Install icon
install -m 644 Resources/legion-toolkit.svg %{buildroot}%{_datadir}/icons/hicolor/scalable/apps/

# Install systemd services
install -m 644 Scripts/legion-toolkit.service %{buildroot}%{_unitdir}/
install -m 644 Scripts/legion-toolkit-system.service %{buildroot}%{_unitdir}/

# Install man page if available
[ -f Documentation/legion-toolkit.1 ] && install -m 644 Documentation/legion-toolkit.1 %{buildroot}%{_mandir}/man1/

%files
%license LICENSE
%doc README.md
/opt/legion-toolkit
%{_bindir}/legion-toolkit
%{_datadir}/applications/legion-toolkit.desktop
%{_datadir}/icons/hicolor/scalable/apps/legion-toolkit.svg
%{_unitdir}/legion-toolkit.service
%{_unitdir}/legion-toolkit-system.service
%{_sysconfdir}/legion-toolkit
%{_mandir}/man1/legion-toolkit.1*

%post
%systemd_post legion-toolkit.service legion-toolkit-system.service
# Update icon cache
touch --no-create %{_datadir}/icons/hicolor &>/dev/null || :

%preun
%systemd_preun legion-toolkit.service legion-toolkit-system.service

%postun
%systemd_postun_with_restart legion-toolkit.service legion-toolkit-system.service
# Update icon cache
if [ $1 -eq 0 ] ; then
    touch --no-create %{_datadir}/icons/hicolor &>/dev/null
    gtk-update-icon-cache %{_datadir}/icons/hicolor &>/dev/null || :
fi

%posttrans
gtk-update-icon-cache %{_datadir}/icons/hicolor &>/dev/null || :

%changelog
* Mon Sep 23 2025 Legion Toolkit Team <noreply@example.com> - 1.0.0-1
- Initial release for Linux
- Full feature parity with Windows version
- Support for legion-laptop kernel module
- Multi-distribution support