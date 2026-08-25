using System.Security.Cryptography.X509Certificates;

namespace Net.Mqtt.ReactiveOrm.Security;

public interface IClientCertificateProvider : IDisposable
{
    ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default);
    event EventHandler? CertificateChanged;
}

public abstract class ClientCertificateProviderBase : IClientCertificateProvider
{
    public event EventHandler? CertificateChanged;
    public abstract ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default);
    protected void NotifyCertificateChanged() => CertificateChanged?.Invoke(this, EventArgs.Empty);
    public virtual void Dispose() { }
}

public sealed class PfxCertificateProvider : ClientCertificateProviderBase
{
    private readonly string _path;
    private readonly string? _password;
    private readonly FileSystemWatcher _watcher;

    public PfxCertificateProvider(string path, string? password = null)
    {
        _path = Path.GetFullPath(path);
        _password = password;
        _watcher = CreateWatcher(_path, NotifyCertificateChanged);
    }

    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(X509CertificateLoader.LoadPkcs12FromFile(
            _path, _password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet));
    }

    public override void Dispose() => _watcher.Dispose();

    internal static FileSystemWatcher CreateWatcher(string path, Action changed)
    {
        var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        FileSystemEventHandler onChange = (_, _) => changed();
        RenamedEventHandler onRename = (_, _) => changed();
        watcher.Changed += onChange;
        watcher.Created += onChange;
        watcher.Renamed += onRename;
        return watcher;
    }
}

public sealed class PemCertificateProvider : ClientCertificateProviderBase
{
    private readonly string _certificatePath;
    private readonly string _privateKeyPath;
    private readonly FileSystemWatcher _certificateWatcher;
    private readonly FileSystemWatcher _keyWatcher;

    public PemCertificateProvider(string certificatePath, string privateKeyPath)
    {
        _certificatePath = Path.GetFullPath(certificatePath);
        _privateKeyPath = Path.GetFullPath(privateKeyPath);
        _certificateWatcher = PfxCertificateProvider.CreateWatcher(_certificatePath, NotifyCertificateChanged);
        _keyWatcher = PfxCertificateProvider.CreateWatcher(_privateKeyPath, NotifyCertificateChanged);
    }

    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(X509Certificate2.CreateFromPemFile(_certificatePath, _privateKeyPath));
    }

    public override void Dispose()
    {
        _certificateWatcher.Dispose();
        _keyWatcher.Dispose();
    }
}

public sealed class StoreCertificateProvider(
    string thumbprint,
    StoreName storeName = StoreName.My,
    StoreLocation storeLocation = StoreLocation.CurrentUser) : ClientCertificateProviderBase
{
    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: true);
        if (certificates.Count == 0)
            throw new InvalidOperationException($"No valid client certificate with thumbprint '{thumbprint}' was found.");
        return ValueTask.FromResult(certificates[0]);
    }
}

public sealed class SecretCertificateProvider(
    Func<CancellationToken, ValueTask<X509Certificate2>> resolver) : ClientCertificateProviderBase
{
    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default) =>
        resolver(cancellationToken);

    public void SignalRotation() => NotifyCertificateChanged();
}

public sealed record CertificateExpiringEvent(
    string Subject,
    string Thumbprint,
    DateTimeOffset ExpiresAt,
    TimeSpan Remaining);
