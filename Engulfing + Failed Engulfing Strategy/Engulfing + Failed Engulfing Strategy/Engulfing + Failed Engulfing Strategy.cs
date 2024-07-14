using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class EngulfingFailedEngulfingStrategy : Robot
    {
        [Parameter("Start Time [XX:XX]", DefaultValue = "10:00", Group = "Times")]
        public string StartTime { get; set; }

        [Parameter("End Time [XX:XX]", DefaultValue = "10:00", Group = "Times")]
        public string EndTime { get; set; }

        [Parameter("Risk Type", DefaultValue = RiskType.FixedLot, Group = "Trade Settings")]
        public RiskType RiskStrategy { get; set; }

        [Parameter("Volume for fixed lot", DefaultValue = 1, Group = "Trade Settings")]
        public double Volume { get; set; }

        [Parameter("% Risk", DefaultValue = 1, Group = "Trade Settings")]
        public double PercentRisk { get; set; }

        [Parameter("Take Profit Method", DefaultValue = TakeProfitType.RiskReward, Group = "Trade Settings")]
        public TakeProfitType TakeProfitStrategy { get; set; }

        [Parameter("Risk Reward Ratio", DefaultValue = 1, Group = "Trade Settings")]
        public double RiskReward { get; set; }

        [Parameter("Take Profit Pips", DefaultValue = 10, Group = "Trade Settings")]
        public double TakeProfitPips { get; set; }

        [Parameter("Stop Loss Method", DefaultValue = StopLossType.EntryCandle, Group = "Trade Settings")]
        public StopLossType StopLossStrategy { get; set; }

        [Parameter("Stop Loss Pips", DefaultValue = 10, Group = "Trade Settings")]
        public double StopLossPips { get; set; }

        [Parameter("Use 4h Confluence", DefaultValue = true, Group = "Trade Settings")]
        public bool FourHourSafety { get; set; }

        public enum RiskType
        {
            FixedLot,
            PercentageRisk
        }

        public enum TakeProfitType
        {
            Pips,
            RiskReward
        }

        public enum StopLossType
        {
            Pips,
            EntryCandle
        }

        private Bars fifteenMinBars;
        private Bars fourHourBars;

        private List<Bar> fourHourBullBars = new();
        private List<Bar> fourHourBearBars = new();

        private List<Bar> bullEngBars = new List<Bar>();
        private List<Bar> bearEngBars = new List<Bar>();
        private Dictionary<Bar, Bar> bullComboBars = new();
        private Dictionary<Bar, Bar> bearComboBars = new();
        private List<Bar> invalidatedBars = new();

        private TimeOnly _startTime = new();
        private TimeOnly _endTime = new();

        protected override void OnStart()
        {
            fifteenMinBars = MarketData.GetBars(TimeFrame.Minute15, Symbol.Name);
            fourHourBars = MarketData.GetBars(TimeFrame.Hour4, Symbol.Name);

            _startTime = ConvertToTime(StartTime);
            _endTime = ConvertToTime(EndTime);
        }

        protected override void OnTick()
        {
            if (IsTradingTime())
            {
                EnterBuys(bullComboBars);
                EnterSells(bearComboBars);
            }
        }

        protected override void OnStop()
        {
            // Handle cBot stop here
        }

        protected override void OnBar()
        {
            Chart.RemoveAllObjects();

            bullEngBars.Clear();
            bearEngBars.Clear();
            bullComboBars.Clear();
            bearComboBars.Clear();
            fourHourBearBars.Clear();
            fourHourBullBars.Clear();

            FourHourBulls(fourHourBars);
            FourHourBears(fourHourBars);

            BullishEngulfings(fifteenMinBars);
            BearishEngulfings(fifteenMinBars);

            BullishEngWithEngFailFinder(bullEngBars, fifteenMinBars);
            BearishEngWithEngFailFinder(bearEngBars, fifteenMinBars);

            DrawBullEngs(bullComboBars);
            DrawBearEngs(bearComboBars);
        }

        #region Times
        private TimeOnly ConvertToTime(string timeString)
        {
            try
            {
                TimeOnly time = TimeOnly.ParseExact(timeString, "HH:mm");
                return time;
            }
            catch (Exception ex)
            {
                Print($"Error: {ex.Message} Please enter a valid time.");
                Stop();
                return TimeOnly.MinValue;
            }
        }

        private bool IsTradingTime()
        {
            var serverTime = TimeOnly.FromDateTime( Server.Time );
            if ( serverTime >= _startTime && serverTime < _endTime)
            {
                return true;
            }

            return false;
        }
        #endregion

        #region Risk
        private double CalculateStopLoss(Bar bar)
        {
            if (StopLossStrategy == StopLossType.EntryCandle)
            {
                var priceDiff = Math.Abs(bar.High - bar.Low);
                return priceDiff / Symbol.PipValue;
            }
            else
                return StopLossPips;
        }

        private double CalculateTP(Bar bullBar, double stopLossPips)
        {
            if (TakeProfitStrategy == TakeProfitType.RiskReward)
            {
                return stopLossPips * RiskReward;
            }
            else
                return TakeProfitPips;
        }

        private double CalculateVolume(double stopLossPips)
        {
            if (RiskStrategy == RiskType.PercentageRisk)
            {
                var bruh = Symbol.VolumeForFixedRisk((PercentRisk * Account.Balance) / 100, stopLossPips);
                return bruh;
            }
            else
            {
                return Symbol.QuantityToVolumeInUnits(Volume);
            }
        }

        private void OpenPosition(Bar bar, TradeType tradeType)
        {
            var stopLoss = CalculateStopLoss(bar);
            var takeProfit = CalculateTP(bar, stopLoss);
            var volume = CalculateVolume(stopLoss);
            ExecuteMarketOrder(tradeType, Symbol.Name, volume, "Squidward", stopLoss, takeProfit);
            invalidatedBars.Add(bar);
        }
        #endregion

        //===============================================================================================================

        #region Bullish engulfings
        public bool IsBear(Bars bars, int barIndex) => (bars.ClosePrices[barIndex] < bars.OpenPrices[barIndex]);//check if current candle is bearish

        public bool IsNextCandleBull(Bars bars, int barIndex) => (bars.ClosePrices[barIndex + 1] > bars.OpenPrices[barIndex + 1]);//takes the set of bars to go through, with the index, checking if the candle after is a bull

        public bool IsBullishEngulfing(Bars bars, int barIndex, int forLoopIndex)//IsBear + IsNextCandleBull + IsBullishEngulfing(which takes 2 or more candles to engulf)forloopindex is barindex + 2
        {
            for (int i = forLoopIndex; i < bars.Count - 1; i++)
            {
                if (IsBear(bars, i))
                    return false;
                if (bars.ClosePrices[i] > bars.HighPrices[barIndex])//SHOULD BE IIIIIIIIIIIIIIIIIII in the if condition
                    return true;
            }
            return false;
        }

        public bool BullishEngChecks(Bars bars, int i, int forLoopIndex) => (IsBear(bars, i) && IsNextCandleBull(bars, i) && IsBullishEngulfing(bars, i, forLoopIndex));//IsBear + IsNextCandleBull + IsBullishEngulfing

        public bool DoesBullishEngulfingFail(Bars bars, int barIndex, int forLoopIndex)//Check if any given bullish engulfing fails
        {
            for (int i = forLoopIndex; i < bars.Count - 1; i++)
            {
                if (bars.ClosePrices[i] < bars.LowPrices[barIndex])
                {
                    return true;
                }
            }
            return false;
        }

        private void BullishEngulfings(Bars bars)
        {
            var count = 0;
            for(int i = bars.Count - 1; i > 0; i--)
            {
                if(BullishEngChecks(bars, i, i + 1) && !DoesBullishEngulfingFail(bars, i, i + 1) && count < 50)
                {
                    bullEngBars.Add(bars[i]);
                }
                count++;
            }
        }

        private void DrawBullEngs(Dictionary<Bar, Bar> bullComboBars)
        {
            var engBars = bullComboBars.Keys.ToList();
            foreach (Bar bar in engBars)
            {
                Chart.DrawRectangle($"BullEngBar {bar.OpenTime}", bar.OpenTime, bar.Low, Server.Time, bar.High, Color.Green);
            }
            var bearBars = bullComboBars.Values.ToList();
            foreach (Bar bar in bearBars)
            {
                Chart.DrawRectangle($"BearEngBar {bar.OpenTime}", bar.OpenTime, bar.Low, Server.Time, bar.High, Color.Red);
            }
        }
        #endregion

        #region Bullish Combo Finder
        private void BullishEngWithEngFailFinder(List<Bar> engBars, Bars bars)
        {
            foreach (Bar bar in engBars)
            {
                var index = fifteenMinBars.OpenTimes.GetIndexByExactTime(bar.OpenTime);
                for (int i = index - 1; i > 0; i--)
                {
                    if (BullishEngChecks(bars, i, i + 1))
                        break;

                    else if (BearishEngChecks(bars, i, i + 1) && DoesBearishEngulfingFail(bars, i, i + 1) && IsBullLowerThanBear(bar, bars[i]))
                    {
                        if (FourHourSafety)
                        {
                            if(IsBuySafe(i, index))
                                bullComboBars.Add(bar, bars[i]);
                            break;
                        }
                        bullComboBars.Add(bar, bars[i]);
                        break;
                    }
                }
            }
        }

        private bool IsBullLowerThanBear(Bar bullBar, Bar bearBar) => bullBar.Low < bearBar.Low;
        #endregion

        #region Buy entry
        private void EnterBuys(Dictionary<Bar, Bar> bullComboBars)
        {
            var ask = Symbol.Ask;
            Bar mostRecentCombo = new();
            if (bullComboBars.Any())
               mostRecentCombo = bullComboBars.Keys.First();

            if (ask == mostRecentCombo.High && !invalidatedBars.Contains(mostRecentCombo))
            {
                OpenPosition(mostRecentCombo, TradeType.Buy);
            }
        }
        #endregion

        //========================================================================================

        #region Bearish engulfings
        public bool IsBull(Bars bars, int barIndex) => (bars.ClosePrices[barIndex] > bars.OpenPrices[barIndex]);//check if current candle is bearish

        public bool IsNextCandleBear(Bars bars, int barIndex) => (bars.ClosePrices[barIndex + 1] < bars.OpenPrices[barIndex + 1]);//Checks if next candle after this bull is bearish

        public bool IsBearishEngulfing(Bars bars, int barIndex, int forLoopIndex)//IsBear + IsNextCandleBull + IsBullishEngulfing(which takes 2 or more candles to engulf)
        {
            for (int i = forLoopIndex; i < bars.Count - 1; i++)
            {
                if (IsBull(bars, i))
                    return false;
                if (bars.ClosePrices[i] < bars.LowPrices[barIndex])//SHOULD BE IIIIIIIIIIIIIIIIIII in the if condition
                    return true;
            }
            return false;
        }

        public bool BearishEngChecks(Bars bars, int i, int forLoopIndex) => (IsBull(bars, i) && IsNextCandleBear(bars, i) && IsBearishEngulfing(bars, i, forLoopIndex));

        public bool DoesBearishEngulfingFail(Bars bars, int barIndex, int forLoopIndex)
        {
            for (int i = forLoopIndex; i < bars.Count - 1; i++)
            {
                if (bars.ClosePrices[i] > bars.HighPrices[barIndex])
                {
                    return true;
                }
            }
            return false;
        } //Check if any given bullish engulfing fails

        private void BearishEngulfings(Bars bars)
        {
            var count = 0;
            for (int i = bars.Count - 1; i > 0; i--)
            {
                if (BearishEngChecks(bars, i, i + 1) && !DoesBearishEngulfingFail(bars, i, i + 1) && count < 50)
                {
                    bearEngBars.Add(bars[i]);
                }
                count++;
            }
        }

        private void DrawBearEngs(Dictionary<Bar, Bar> bearComboBars)
        {
            var engBars = bearComboBars.Keys.ToList();
            foreach (Bar bar in engBars)
            {
                Chart.DrawRectangle($"BearEngBar {bar.OpenTime}", bar.OpenTime, bar.Low, Server.Time, bar.High, Color.Red);
            }
            var bearBars = bearComboBars.Values.ToList();
            foreach (Bar bar in bearBars)
            {
                Chart.DrawRectangle($"BullEngBar {bar.OpenTime}", bar.OpenTime, bar.Low, Server.Time, bar.High, Color.Green);
            }
        }
        #endregion

        #region Bearish Combo
        private void BearishEngWithEngFailFinder(List<Bar> engBars, Bars bars)
        {
            foreach (Bar bar in engBars)
            {
                var index = fifteenMinBars.OpenTimes.GetIndexByExactTime(bar.OpenTime);
                for (int i = index - 1; i > 0; i--)
                {
                    if (BearishEngChecks(bars, i, i + 1))
                        break;

                    else if (BullishEngChecks(bars, i, i + 1) && DoesBullishEngulfingFail(bars, i, i + 1) && IsBearHigherThanBull(bar, bars[i]))
                    {
                        if (FourHourSafety)
                        {
                            if (IsSellSafe(i, index))
                                bearComboBars.Add(bar, bars[i]);
                            break;
                        }
                        bearComboBars.Add(bar, bars[i]);
                        break;
                    }
                }
            }
        }

        private bool IsBearHigherThanBull(Bar bearBar, Bar bullBar) => bearBar.High > bullBar.High;
        #endregion

        #region Sell entry
        private void EnterSells(Dictionary<Bar, Bar> bearComboBars)
        {
            var bid = Symbol.Bid;
            Bar mostRecentCombo = new();
            if (bearComboBars.Any())
                mostRecentCombo = bearComboBars.Keys.First();

            if (bid == mostRecentCombo.Low && !invalidatedBars.Contains(mostRecentCombo))
            {
                OpenPosition(mostRecentCombo, TradeType.Sell);
            }
        }
        #endregion

        //==========================================================================================


        private void FourHourBears(Bars bars)
        {
            var count = 0;
            for (int i = bars.Count - 1; i > 0; i--)
            {
                if (BearishEngChecks(bars, i, i + 1) && !DoesBearishEngulfingFail(bars, i, i + 1) && count < 50)
                {
                    fourHourBearBars.Add(bars[i]);
                }
                count++;
            }
        }

        private bool IsSellSafe(int startIndex, int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                var bar = fifteenMinBars[i];
                foreach(Bar fourHourBar in fourHourBearBars)
                {
                    if (bar.High >= fourHourBar.Low)
                    {
                        Chart.DrawRectangle("bruh", fourHourBar.OpenTime, fourHourBar.Low, Server.Time, fourHourBar.High, Color.Purple);
                        return true;
                    }
                }
            }
            return false;
        }


        private void FourHourBulls(Bars bars)
        {
            var count = 0;
            for (int i = bars.Count - 1; i > 0; i--)
            {
                if (BullishEngChecks(bars, i, i + 1) && !DoesBullishEngulfingFail(bars, i, i + 1) && count < 50)
                {
                    fourHourBullBars.Add(bars[i]);
                }
                count++;
            }
        }

        private bool IsBuySafe(int startIndex, int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                var bar = fifteenMinBars[i];
                foreach (Bar fourHourBar in fourHourBullBars)
                {
                    if (bar.Low <= fourHourBar.High)
                    {
                        Chart.DrawRectangle("bruh", fourHourBar.OpenTime, fourHourBar.Low, Server.Time, fourHourBar.High, Color.Blue);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}