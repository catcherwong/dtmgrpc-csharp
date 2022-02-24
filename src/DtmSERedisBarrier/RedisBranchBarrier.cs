﻿using DtmCommon;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DtmSERedisBarrier
{
    public static class RedisBranchBarrier
    {
        public static async Task RedisCheckAdjustAmount(this BranchBarrier bb, StackExchange.Redis.IDatabase redisClient, string key, int amount, int barrierExpire)
        {
            bb.BarrierID = bb.BarrierID + 1;
            var bid = bb.BarrierID.ToString().PadLeft(2, '0');
            var bkey1 = $"{bb.Gid}-{bb.BranchID}-{bb.Op}-{bid}";

            var originOp = Constant.Barrier.OpDict.TryGetValue(bb.Op, out var ot) ? ot : string.Empty;
            var bkey2 = $"{bb.Gid}-{bb.BranchID}-{originOp}-{bid}";

            StackExchange.Redis.RedisResult result = null;
            try
            {
                result = await redisClient.ScriptEvaluateAsync(
                Constant.Barrier.REDIS_LUA_CheckAdjustAmount,
                new StackExchange.Redis.RedisKey[] { key, bkey1, bkey2 },
                new StackExchange.Redis.RedisValue[] { amount, originOp, barrierExpire });
            }
            catch (System.Exception ex)
            {
                bb.Logger?.LogWarning(ex, "RedisCheckAdjustAmount lua return :{0}", result);
                throw;
            }

            if (result.IsNull && bb.Op == Constant.TYPE_MSG && result.Equals(Constant.ResultDuplicated))
                throw new DtmDuplicatedException();

            if (result.IsNull && result.Equals(Constant.ResultFailure))
                throw new DtmFailureException();
        }

        public static async Task RedisQueryPrepared(this BranchBarrier bb, StackExchange.Redis.IDatabase redisClient, int barrierExpire)
        {
            var bkey1 = $"{bb.Gid}-{Constant.Barrier.MSG_BRANCHID}-{Constant.TYPE_MSG}-{Constant.Barrier.MSG_BARRIER_ID}";
            StackExchange.Redis.RedisResult result = null;
            try
            {
                result = await redisClient.ScriptEvaluateAsync(
                Constant.Barrier.REDIS_LUA_QueryPrepared,
                new StackExchange.Redis.RedisKey[] { bkey1 },
                new StackExchange.Redis.RedisValue[] { barrierExpire });
            }
            catch (System.Exception ex)
            {
                bb.Logger?.LogWarning(ex, "RedisQueryPrepared lua return :{0}", result);
                throw;
            }

            if (result.IsNull && result.Equals(Constant.ResultFailure))
                throw new DtmFailureException();
        }
    }
}