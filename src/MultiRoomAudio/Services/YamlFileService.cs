using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MultiRoomAudio.Services;

/// <summary>
/// Base class for services that persist data to YAML files.
/// Provides thread-safe load/save operations with standard serialization settings.
/// </summary>
/// <typeparam name="T">The type of data to persist.</typeparam>
public abstract class YamlFileService<T> where T : class, new()
{
    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly object _lock = new();

    protected T Data { get; private set; } = new();

    /// <summary>
    /// Creates a new YAML file service.
    /// </summary>
    /// <param name="filePath">Full path to the YAML file.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    protected YamlFileService(string filePath, ILogger logger)
    {
        _filePath = filePath;
        _logger = logger;

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    /// <summary>
    /// Path to the YAML file.
    /// </summary>
    protected string FilePath => _filePath;

    /// <summary>
    /// Logger instance for derived classes.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Lock object for thread-safe operations. Use this when accessing Data directly.
    /// </summary>
    protected object Lock => _lock;

    /// <summary>
    /// Load data from the YAML file.
    /// </summary>
    /// <returns>True if data was loaded, false if file doesn't exist or is empty.</returns>
    public virtual bool Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("YAML file not found at {Path}, starting with defaults", _filePath);
                Data = new T();
                return false;
            }

            try
            {
                var yaml = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(yaml))
                {
                    _logger.LogDebug("YAML file {Path} is empty, starting with defaults", _filePath);
                    Data = new T();
                    return false;
                }

                Data = _deserializer.Deserialize<T>(yaml) ?? new T();
                OnDataLoaded();
                _logger.LogDebug("Loaded data from {Path}", _filePath);
                return true;
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                _logger.LogError(ex, "Failed to parse YAML from {Path}. File may be malformed", _filePath);
                Data = new T();
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to read {Path}. Check file permissions", _filePath);
                Data = new T();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading from {Path}", _filePath);
                Data = new T();
                return false;
            }
        }
    }

    /// <summary>
    /// Save data to the YAML file.
    /// </summary>
    /// <returns>True if saved successfully.</returns>
    public virtual bool Save()
    {
        lock (_lock)
        {
            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var yaml = _serializer.Serialize(Data);
                File.WriteAllText(_filePath, yaml);
                _logger.LogDebug("Saved data to {Path}", _filePath);
                return true;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to write {Path}. Check disk space and permissions", _filePath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving to {Path}", _filePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Called after data is successfully loaded. Override to perform post-load processing.
    /// Called within the lock, so thread-safe access to Data is guaranteed.
    /// </summary>
    protected virtual void OnDataLoaded()
    {
    }
}

/// <summary>
/// Base class for services that persist dictionary data to YAML files.
/// Provides thread-safe CRUD operations with automatic persistence.
/// </summary>
/// <typeparam name="TKey">The dictionary key type (typically string).</typeparam>
/// <typeparam name="TValue">The dictionary value type.</typeparam>
public abstract class YamlDictionaryService<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly object _lock = new();

    private Dictionary<TKey, TValue> _data = new();

    /// <summary>
    /// Creates a new YAML dictionary service.
    /// </summary>
    /// <param name="filePath">Full path to the YAML file.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    protected YamlDictionaryService(string filePath, ILogger logger)
    {
        _filePath = filePath;
        _logger = logger;

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    /// <summary>
    /// Path to the YAML file.
    /// </summary>
    protected string FilePath => _filePath;

    /// <summary>
    /// Logger instance for derived classes.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Lock object for thread-safe operations.
    /// </summary>
    protected object Lock => _lock;

    /// <summary>
    /// Number of items in the dictionary.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _data.Count;
            }
        }
    }

    /// <summary>
    /// Load data from the YAML file.
    /// </summary>
    /// <returns>True if data was loaded, false if file doesn't exist or is empty.</returns>
    public virtual bool Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogDebug("YAML file not found at {Path}, starting with empty dictionary", _filePath);
                _data = new Dictionary<TKey, TValue>();
                return false;
            }

            try
            {
                var yaml = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(yaml))
                {
                    _logger.LogDebug("YAML file {Path} is empty, starting with empty dictionary", _filePath);
                    _data = new Dictionary<TKey, TValue>();
                    return false;
                }

                _data = _deserializer.Deserialize<Dictionary<TKey, TValue>>(yaml)
                    ?? new Dictionary<TKey, TValue>();

                OnDataLoaded(_data);
                _logger.LogDebug("Loaded {Count} items from {Path}", _data.Count, _filePath);
                return true;
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                _logger.LogError(ex, "Failed to parse YAML from {Path}. File may be malformed", _filePath);
                _data = new Dictionary<TKey, TValue>();
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to read {Path}. Check file permissions", _filePath);
                _data = new Dictionary<TKey, TValue>();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading from {Path}", _filePath);
                _data = new Dictionary<TKey, TValue>();
                return false;
            }
        }
    }

    /// <summary>
    /// Save data to the YAML file.
    /// </summary>
    /// <returns>True if saved successfully.</returns>
    public virtual bool Save()
    {
        lock (_lock)
        {
            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var yaml = _serializer.Serialize(_data);
                File.WriteAllText(_filePath, yaml);
                _logger.LogDebug("Saved {Count} items to {Path}", _data.Count, _filePath);
                return true;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to write {Path}. Check disk space and permissions", _filePath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving to {Path}", _filePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Get a value by key.
    /// </summary>
    public TValue? Get(TKey key)
    {
        lock (_lock)
        {
            return _data.TryGetValue(key, out var value) ? value : default;
        }
    }

    /// <summary>
    /// Try to get a value by key.
    /// </summary>
    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }
            value = default;
            return false;
        }
    }

    /// <summary>
    /// Set a value by key and optionally save.
    /// </summary>
    public bool Set(TKey key, TValue value, bool save = true)
    {
        lock (_lock)
        {
            _data[key] = value;
            OnItemSet(key, value);
            return save ? Save() : true;
        }
    }

    /// <summary>
    /// Remove a value by key and optionally save.
    /// </summary>
    public bool Remove(TKey key, bool save = true)
    {
        lock (_lock)
        {
            if (!_data.Remove(key))
                return false;

            OnItemRemoved(key);
            return save ? Save() : true;
        }
    }

    /// <summary>
    /// Check if a key exists.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        lock (_lock)
        {
            return _data.ContainsKey(key);
        }
    }

    /// <summary>
    /// Get all keys.
    /// </summary>
    public IReadOnlyList<TKey> GetKeys()
    {
        lock (_lock)
        {
            return _data.Keys.ToList();
        }
    }

    /// <summary>
    /// Get all values.
    /// </summary>
    public IReadOnlyList<TValue> GetValues()
    {
        lock (_lock)
        {
            return _data.Values.ToList();
        }
    }

    /// <summary>
    /// Get a snapshot of all data as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<TKey, TValue> GetAll()
    {
        lock (_lock)
        {
            return new Dictionary<TKey, TValue>(_data);
        }
    }

    /// <summary>
    /// Update an existing item with a function, then optionally save.
    /// </summary>
    public bool Update(TKey key, Action<TValue> updateAction, bool save = true)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(key, out var value))
                return false;

            updateAction(value);
            return save ? Save() : true;
        }
    }

    /// <summary>
    /// Get or create a value with a factory function.
    /// </summary>
    public TValue GetOrCreate(TKey key, Func<TValue> factory, bool save = true)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(key, out var existing))
                return existing;

            var value = factory();
            _data[key] = value;
            OnItemSet(key, value);

            if (save)
                Save();

            return value;
        }
    }

    /// <summary>
    /// Execute an action within the lock for complex operations.
    /// </summary>
    protected TResult WithLock<TResult>(Func<Dictionary<TKey, TValue>, TResult> action)
    {
        lock (_lock)
        {
            return action(_data);
        }
    }

    /// <summary>
    /// Execute an action within the lock for complex operations.
    /// </summary>
    protected void WithLock(Action<Dictionary<TKey, TValue>> action)
    {
        lock (_lock)
        {
            action(_data);
        }
    }

    /// <summary>
    /// Called after data is successfully loaded. Override to perform post-load processing
    /// like ensuring keys match value fields.
    /// Called within the lock.
    /// </summary>
    protected virtual void OnDataLoaded(Dictionary<TKey, TValue> data)
    {
    }

    /// <summary>
    /// Called after an item is set. Override for logging or validation.
    /// Called within the lock.
    /// </summary>
    protected virtual void OnItemSet(TKey key, TValue value)
    {
    }

    /// <summary>
    /// Called after an item is removed. Override for logging.
    /// Called within the lock.
    /// </summary>
    protected virtual void OnItemRemoved(TKey key)
    {
    }
}
