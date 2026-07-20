// /iap/GetSteamWalletCurrency  ReqPacket_GetSteamWalletCurrency -> ResPacket_GetSteamWalletCurrency
// Types shared with other endpoints live in _shared.cs.

public class ReqPacket_GetSteamWalletCurrency
{
    public string steamId;
    public long PacketId;
}

public class ResPacket_GetSteamWalletCurrency
{
    public string walletCurrency;
    public long PacketId;
    public string WalletCurrency;
}

