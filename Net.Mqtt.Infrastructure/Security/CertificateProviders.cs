using System.Security.Cryptography.X509Certificates;

namespace Net.Mqtt.Infrastructure.Security;

/// <summary>Defines iclient certificate provider.</summary>
public interface IClientCertificateProvider : IDisposable
{
    /// <summary>Gets the get certificate async operation.</summary>
    ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default);
    /// <summary>Occurs when certificate changed changes.</summary>
    event EventHandler? CertificateChanged;
}

/// <summary>Represents client certificate provider base.</summary>
public abstract class ClientCertificateProviderBase : IClientCertificateProvider
{
    /// <summary>Occurs when certificate changed changes.</summary>
    public event EventHandler? CertificateChanged;
    /// <summary>Gets the get certificate async operation.</summary>
    public abstract ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default);
    /// <summary>Executes the notify certificate changed operation.</summary>
    protected void NotifyCertificateChanged() => CertificateChanged?.Invoke(this, EventArgs.Empty);
    /// <summary>Releases resources used by the dispose operation.</summary>
    public virtual void Dispose() { }
}

/// <summary>Represents pfx certificate provider.</summary>
public sealed class PfxCertificateProvider : ClientCertificateProviderBase
{
    private readonly string _path;
    private readonly string? _password;
    private readonly FileSystemWatcher _watcher;

    /// <summary>Executes the pfx certificate provider operation.</summary>
    public PfxCertificateProvider(string path, string? password = null)
    {
        _path = Path.GetFullPath(path);
        _password = password;
        _watcher = CreateWatcher(_path, NotifyCertificateChanged);
    }

    /// <summary>Gets the get certificate async operation.</summary>
    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(X509CertificateLoader.LoadPkcs12FromFile(
            _path, _password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet));
    }

    /// <summary>Releases resources used by the dispose operation.</summary>
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

/// <summary>Represents pem certificate provider.</summary>
public sealed class PemCertificateProvider : ClientCertificateProviderBase
{
    private readonly string _certificatePath;
    private readonly string _privateKeyPath;
    private readonly FileSystemWatcher _certificateWatcher;
    private readonly FileSystemWatcher _keyWatcher;

    /// <summary>Executes the pem certificate provider operation.</summary>
    public PemCertificateProvider(string certificatePath, string privateKeyPath)
    {
        _certificatePath = Path.GetFullPath(certificatePath);
        _privateKeyPath = Path.GetFullPath(privateKeyPath);
        _certificateWatcher = PfxCertificateProvider.CreateWatcher(_certificatePath, NotifyCertificateChanged);
        _keyWatcher = PfxCertificateProvider.CreateWatcher(_privateKeyPath, NotifyCertificateChanged);
    }

    /// <summary>Gets the get certificate async operation.</summary>
    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(X509Certificate2.CreateFromPemFile(_certificatePath, _privateKeyPath));
    }

    /// <summary>Releases resources used by the dispose operation.</summary>
    public override void Dispose()
    {
        _certificateWatcher.Dispose();
        _keyWatcher.Dispose();
    }
}

/// <summary>Represents store certificate provider.</summary>
public sealed class StoreCertificateProvider(
    string thumbprint,
    StoreName storeName = StoreName.My,
    StoreLocation storeLocation = StoreLocation.CurrentUser) : ClientCertificateProviderBase
{
    /// <summary>Gets the get certificate async operation.</summary>
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

/// <summary>Represents secret certificate provider.</summary>
public sealed class SecretCertificateProvider(
    Func<CancellationToken, ValueTask<X509Certificate2>> resolver) : ClientCertificateProviderBase
{
    /// <summary>Gets the get certificate async operation.</summary>
    public override ValueTask<X509Certificate2> GetCertificateAsync(CancellationToken cancellationToken = default) =>
        resolver(cancellationToken);

    /// <summary>Executes the signal rotation operation.</summary>
    public void SignalRotation() => NotifyCertificateChanged();
}

/// <summary>Describes a client certificate that is approaching expiration.</summary>
/// <param name="Subject">The certificate subject.</param>
/// <param name="Thumbprint">The certificate thumbprint.</param>
/// <param name="ExpiresAt">The certificate expiration time.</param>
/// <param name="Remaining">The remaining lifetime when the event was raised.</param>
public sealed record CertificateExpiringEvent(
    string Subject,
    string Thumbprint,
    DateTimeOffset ExpiresAt,
    TimeSpan Remaining);
