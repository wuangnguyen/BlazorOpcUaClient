﻿using Opc.Ua;
using Opc.Ua.Client;

namespace OPC_UA_Client.Services;

/// <summary>
/// Service for polling OPC UA nodes at regular intervals.
/// </summary>
public class OpcUaPollingService : IAsyncDisposable
{
    private readonly ILogger<OpcUaPollingService> logger;
    private readonly Lazy<Task<Session>> lazySession;
    private Timer? pollingTimer;
    private Session session;
    private NodeId[]? nodeIds;

    /// <summary>
    /// Event triggered when data changes for a monitored node.
    /// </summary>
    public event Action<NodeId, DataValue> OnDataChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpcUaPollingService"/> class.
    /// </summary>
    /// <param name="sessionProvider">The session provider.</param>
    /// <param name="logger">The logger.</param>
    public OpcUaPollingService(OpcUaSessionProvider sessionProvider, ILogger<OpcUaPollingService> logger)
    {
        this.logger = logger;
        lazySession = new Lazy<Task<Session>>(sessionProvider.CreateSessionAsync);
    }

    /// <summary>
    /// Starts polling a single OPC UA node.
    /// </summary>
    /// <param name="nodeId">The node ID to poll.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    public async Task StartPollingAsync(NodeId nodeId, int interval = 1000)
    {
        await StartPollingAsync([nodeId], interval);
    }

    /// <summary>
    /// Starts polling multiple OPC UA nodes.
    /// </summary>
    /// <param name="nodeIds">The node IDs to poll.</param>
    /// <param name="interval">The polling interval in milliseconds.</param>
    public async Task StartPollingAsync(NodeId[] nodeIds, int interval = 1000)
    {
        session = await lazySession.Value;
        
        this.nodeIds = nodeIds;

        pollingTimer = new Timer(async _ => await ReadValuesAsync(), null, 0, interval);
    }

    /// <summary>
    /// Reads values from the monitored nodes.
    /// </summary>
    private async Task ReadValuesAsync()
    {
        if (nodeIds == null || nodeIds.Length == 0)
        {
            return;
        }

        try
        {
            var (readValues, results) = await session.ReadValuesAsync(nodeIds);

            for (int i = 0; i < nodeIds.Length; i++)
            {
                var value = readValues[i];
                if (value != null && StatusCode.IsGood(value.StatusCode))
                {
                    OnDataChanged?.Invoke(nodeIds[i], value);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error fetching data for NodeIds: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the service asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (pollingTimer != null)
        {
            await pollingTimer.DisposeAsync();
        }
    }
}
