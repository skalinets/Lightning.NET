﻿using System;
using static LightningDB.Native.Lmdb;

namespace LightningDB;

/// <summary>
/// Lightning database.
/// </summary>
public sealed class LightningDatabase : IDisposable
{
    internal uint _handle;
    private bool _disposed = false;
    private readonly DatabaseConfiguration _configuration;
    private readonly bool _closeOnDispose;
    private readonly LightningTransaction _transaction;
    private readonly IDisposable _pinnedConfig;

    /// <summary>
    /// Creates a LightningDatabase instance.
    /// </summary>
    /// <param name="name">Database name.</param>
    /// <param name="transaction">Active transaction.</param>
    /// <param name="configuration">Options for the database, like encoding, option flags, and comparison logic.</param>
    /// <param name="closeOnDispose">Close database handle on dispose</param>
    internal LightningDatabase(string name, LightningTransaction transaction, DatabaseConfiguration configuration,
        bool closeOnDispose)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));

        Name = name;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _closeOnDispose = closeOnDispose;
        Environment = transaction.Environment;
        _transaction = transaction;
        mdb_dbi_open(transaction._handle, name, _configuration.Flags, out _handle).ThrowOnError();
        _pinnedConfig = _configuration.ConfigureDatabase(transaction, this);
        IsOpened = true;
    }

    /// <summary>
    /// Whether the database handle has been release from Dispose, or from unsuccessful OpenDatabase call.
    /// </summary>
    public bool IsReleased => _handle == default;

    /// <summary>
    /// Is database opened.
    /// </summary>
    public bool IsOpened { get; private set; }

    public Stats DatabaseStats
    {
        get
        {
            mdb_stat(_transaction._handle, _handle, out var nativeStat).ThrowOnError();
            return new Stats
            {
                BranchPages = nativeStat.ms_branch_pages,
                BTreeDepth = nativeStat.ms_depth,
                Entries = nativeStat.ms_entries,
                LeafPages = nativeStat.ms_leaf_pages,
                OverflowPages = nativeStat.ms_overflow_pages,
                PageSize = nativeStat.ms_psize
            };
        }
    }

    /// <summary>
    /// Database name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Environment in which the database was opened.
    /// </summary>
    public LightningEnvironment Environment { get; }

    /// <summary>
    /// Drops the database.
    /// </summary>
    public MDBResultCode Drop(LightningTransaction transaction)
    {
        var result = mdb_drop(transaction._handle, _handle, true);
        IsOpened = false;
        _handle = default;
        return result;
    }

    /// <summary>
    /// Truncates all data from the database.
    /// </summary>
    public MDBResultCode Truncate(LightningTransaction transaction)
    {
        return mdb_drop(transaction._handle, _handle, false);
    }

    /// <summary>
    /// Deallocates resources opened by the database.
    /// </summary>
    /// <param name="disposing">true if called from Dispose.</param>
    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        _disposed = true;

        IsOpened = false;
        _pinnedConfig?.Dispose();

        if (_closeOnDispose)
            mdb_dbi_close(Environment._handle, _handle);

        GC.SuppressFinalize(this);
        _handle = default;
    }

    /// <summary>
    /// Deallocates resources opened by the database.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    ~LightningDatabase()
    {
        Dispose(false);
    }
}