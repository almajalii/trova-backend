using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.BankConnection;
using TrovaBackend.Services.CapabilityScore;

namespace TrovaBackend.Services.BankConnection;

public interface IBankConnectionService
{
    Task<ConnectBankResponse> ConnectAsync(Guid userId, ConnectBankRequest request);
}

public class BankConnectionService : IBankConnectionService
{
    private readonly AppDbContext _db;
    private readonly IJofsDataProvider _dataProvider;
    private readonly ICapabilityScoreService _capabilityScoreService;

    public BankConnectionService(AppDbContext db, IJofsDataProvider dataProvider, ICapabilityScoreService capabilityScoreService)
    {
        _db = db;
        _dataProvider = dataProvider;
        _capabilityScoreService = capabilityScoreService;
    }

    public async Task<ConnectBankResponse> ConnectAsync(Guid userId, ConnectBankRequest request)
    {
        if (!TrovaBanks.DisplayNames.TryGetValue(request.BankCode, out var bankName))
            throw new ArgumentException(
                $"Unknown bank code '{request.BankCode}'. Allowed values: {string.Join(", ", TrovaBanks.DisplayNames.Keys)}");

        var snapshot = await _dataProvider.FetchAccountSnapshotAsync(userId, request.BankCode);

        var connection = await _db.BankConnections.FirstOrDefaultAsync(b => b.UserId == userId);
        var isNew = connection == null;
        connection ??= new Models.BankConnection { UserId = userId };

        connection.BankCode = request.BankCode;
        connection.BankName = bankName;

        // Bank-verified — from the provider
        connection.AccountAddress = snapshot.AccountAddress;
        connection.AccountCurrency = snapshot.AccountCurrency;
        connection.AccountStatus = snapshot.AccountStatus;
        connection.AvailableBalanceAmount = snapshot.AvailableBalanceAmount;
        connection.NumberOfCurrentDebts = snapshot.NumberOfCurrentDebts;
        connection.AverageMonthlyCashflowChangePercent = snapshot.AverageMonthlyCashflowChangePercent;

        // Self-reported — from the user, captured on this same screen
        connection.RemainingDebtCapacityJod = request.RemainingDebtCapacityJod;
        connection.NumberOfDelinquentDebts = request.NumberOfDelinquentDebts;

        connection.LastSyncedAt = DateTime.UtcNow;

        if (isNew)
            _db.BankConnections.Add(connection);

        await _db.SaveChangesAsync();

        // This is the trigger point: connecting a bank immediately
        // (re)calculates the capability score using the fresh snapshot.
        await _capabilityScoreService.RecalculateAsync(userId);

        return new ConnectBankResponse { BankName = bankName };
    }
}