﻿using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace ICO_Template
{
    public class ICO_Template : FunctionCode
    {
        public static string Name() => "name of the token";
        public static string Symbol() => "SymbolOfTheToken";
        public const byte Decimals = 8;
        public static readonly byte[] Owner = { 2, 133, 234, 182, 95, 74, 1, 38, 228, 184, 91, 78, 93, 139, 126, 48, 58, 255, 126, 251, 54, 13, 89, 95, 46, 49, 137, 187, 144, 72, 122, 213, 170 };

        private const ulong pre_ico_cap = 30000000_00000000;

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], byte[], BigInteger> Refund;

        public static Object Main(object firstArg, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                byte[] signature = (byte[])firstArg;
                return VerifySignature(Owner, signature);
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                string operation = (string)firstArg;
                if (operation == "deploy") return Deploy();
                if (operation == "mintTokens") return MintTokens();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "transfer")
                {
                    if (args.Length != 3) return false;
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1) return 0;
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "decimals") return Decimals;
            }
            return false;
        }

        // initialization parameters, only once
        // 初始化参数
        public static bool Deploy()
        {
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (total_supply.Length != 0) return false;
            Storage.Put(Storage.CurrentContext, Owner, pre_ico_cap);
            Storage.Put(Storage.CurrentContext, "totalSupply", pre_ico_cap);
            return true;
        }

        // The function MintTokens is only usable by the chosen wallet
        // contract to mint a number of tokens proportional to the
        // amount of neo sent to the wallet contract. The function
        // can only be called during the tokenswap period
        // 将众筹的neo转化为等价的ico代币
        public static bool MintTokens()
        {
            byte[] neo_asset_id = new byte[] { 197, 111, 51, 252, 110, 207, 205, 12, 34, 92, 74, 179, 86, 254, 229, 147, 144, 175, 133, 96, 190, 14, 147, 15, 174, 190, 116, 166, 218, 255, 124, 155 };
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput reference = tx.GetReferences()[0];
            // check whether asset is neo
            // 检查资产是否为neo
            if (reference.AssetId != neo_asset_id) return false;
            byte[] sender = reference.ScriptHash;
            TransactionOutput[] outputs = tx.GetOutputs();
            byte[] receiver = ExecutionEngine.ExecutingScriptHash;
            ulong value = 0;
            // get the total amount of Neo
            // 获取转入智能合约地址的Neo总量
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == receiver)
                {
                    value += (ulong)output.Value;
                }
            }
            // the current exchange rate between ico tokens and neo during the token swap period
            // 获取众筹期间ico token和neo间的转化率
            ulong swap_rate = CurrentSwapRate();
            // crowdfunding failure
            // 众筹失败
            if (swap_rate == 0)
            {
                Refund(sender, receiver, value);
                return false;
            }
            // crowdfunding success
            // 众筹成功
            ulong token = value / 100000000 * swap_rate;
            BigInteger total_token = Storage.Get(Storage.CurrentContext, sender).AsBigInteger();
            Storage.Put(Storage.CurrentContext, sender, token + total_token);
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "totalSupply", token + totalSupply);
            return true;
        }

        // get the total token supply
        // 获取已发行token总量
        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        // function that is always called when someone wants to transfer tokens.
        // 流转token调用
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (!Runtime.CheckWitness(from)) return false;
            if (value < 0) return false;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            BigInteger n_from_value = from_value - value;
            if (n_from_value < 0) return false;
            BigInteger n_to_value = to_value + value;
            Storage.Put(Storage.CurrentContext, from, n_from_value);
            Storage.Put(Storage.CurrentContext, to, n_to_value);
            Transferred(from, to, value);
            return true;
        }

        // get the account balance of another account with address
        // 根据地址获取token的余额
        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        // The function CurrentSwapRate() returns the current exchange rate
        // between ico tokens and neo during the token swap period
        private static ulong CurrentSwapRate()
        {
            BigInteger ico_start_time = 1502726400;
            BigInteger ico_end_time = 1506258000;
            BigInteger total_amount = 1000000000_00000000;
            const ulong rate = 1000_00000000;
            BigInteger total_supply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
            if (total_supply > total_amount)
            {
                return 0;
            }
            uint height = Blockchain.GetHeight();
            uint now = Blockchain.GetHeader(height).Timestamp;
            int time = (int)now - (int)ico_start_time;
            if (time < 0)
            {
                return 0;
            }
            else if (time <= 86400)
            {
                return rate * 130 / 100;
            }
            else if (time <= 259200)
            {
                return rate * 120 / 100;
            }
            else if (time <= 604800)
            {
                return rate * 110 / 100;
            }
            else if (time <= 1209600)
            {
                return rate;
            }
            else
            {
                return 0;
            }
        }
    }
}
