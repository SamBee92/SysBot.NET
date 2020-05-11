using PKHeX.Core;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;
using System;

namespace SysBot.Pokemon
{
    public class RaidBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly RaidSettings Settings;
        public readonly IDumper Dump;
        private readonly bool ldn;
        private readonly bool RaidLog;
        private readonly string FriendCode;
        private readonly bool Roll;
        private readonly bool FRBoth;
        private readonly bool FRAdd;
        private int FriendRemoveCount;
        private readonly int timetojoinraid;

        public RaidBot(PokeBotConfig cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.Raid;
            Dump = Hub.Config.Folder;
            Counts = Hub.Counts;
            ldn = Settings.UseLdnMitm;
            RaidLog = Settings.RaidLog;
            FriendCode = Settings.FriendCode;
            Roll = Settings.AutoRoll;
            FRBoth = Settings.FriendCombined;
            FRAdd = Settings.FriendAdd;
            FriendRemoveCount = Settings.FriendPurge;
            timetojoinraid = Settings.TimeToJoinRaid;
        }

        private int encounterCount;
        private int RaidLogCount;

        protected override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);

            var originalTextSpeed = await EnsureTextSpeedFast(token).ConfigureAwait(false);

            Log("Starting main RaidBot loop.");
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.RaidBot)
            {
                Config.IterateNextRoutine();
                int code = Settings.GetRandomRaidCode();
                bool airplane = await HostRaidAsync(sav, code, token).ConfigureAwait(false);
                await ResetGameAsync(airplane, token).ConfigureAwait(false);

                encounterCount++;
                Log($"Raid host {encounterCount} finished.");
                Counts.AddCompletedRaids();
            }
            await SetTextSpeed(originalTextSpeed, token).ConfigureAwait(false);
        }

        private async Task<bool> HostRaidAsync(SAV8SWSH sav, int code, CancellationToken token)
        {
            if (Roll || (Roll && FRBoth) || (Roll && FRAdd))
            {
                Log("Initializing the rolling auto-host routine.");
                await AutoRollDen(token).ConfigureAwait(false);
            }            
            else if (!Roll && (FRBoth || FRAdd) && (encounterCount > 0 && encounterCount % 3 == 0))
            {
                if ((FRBoth == FRAdd) == true)
                {
                    Log("Enable one or the other, not both. Running FriendAdd instead.");
                    Settings.FriendCombined = false;
                    await FriendAdd(token).ConfigureAwait(false);
                }
                
                if (FRBoth && !FRAdd)
                {
                    await FRAddRemove(token).ConfigureAwait(false);
                }
                
                if (FRAdd && !FRBoth)
                {
                    await FriendAdd(token).ConfigureAwait(false);
                }
            }           
            else if (!Roll && FriendRemoveCount != 0)
            {
                await FriendRemove(token).ConfigureAwait(false);
            }

            // Connect to Y-Comm
            await EnsureConnectedToYComm(Hub.Config, token).ConfigureAwait(false);

            // Press A and stall out a bit for the loading
            await Click(A, 5000, token).ConfigureAwait(false);
            if (!await IsCorrectScreen(CurrentScreen_RaidParty, token).ConfigureAwait(false))
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
            }

            if (code >= 0)
            {
                // Set Link code
                await Click(PLUS, 1000, token).ConfigureAwait(false);
                await EnterTradeCode(code, token).ConfigureAwait(false);

                if (RaidLog == true)
                {
                    RaidLogCount++;
                    System.IO.File.WriteAllText("RaidCode.txt", $"Raid #{RaidLogCount}\n{FriendCode}\nHosting raid as: {sav.OT}\nRaid code is: {code}\n------------------------");
                }
                EchoUtil.Echo($"Raid code is {code}.");

                // Raid barrier here maybe?
                await Click(PLUS, 2_000, token).ConfigureAwait(false);
                await Click(A, 1000, token).ConfigureAwait(false);
            }

            if (code < 0 && RaidLog == true)
            {
                RaidLogCount++;
                System.IO.File.WriteAllText("RaidCode.txt", $"Raid #{RaidLogCount}{Environment.NewLine}\nHosting raid as: {sav.OT}\nRaid is Free For All\n------------------------");
                EchoUtil.Echo($"Raid is Free For All.");
            }

            // Invite others, confirm Pokémon and wait
            await Click(A, 10000, token).ConfigureAwait(false);
            await Click(DUP, 1000, token).ConfigureAwait(false);
            await Click(A, 1000, token).ConfigureAwait(false);

            // Use Offset to actually calculate this value and press A
            var timetowait = (timetojoinraid * 1000) - 3000;
            await Task.Delay(1000, token).ConfigureAwait(false);
            while (timetowait > 0)
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
                timetowait -= 1000;
            }

            await Click(A, 500, token).ConfigureAwait(false);
            Log("Attempting to start the raid.");
            while (!await IsInBattle(token).ConfigureAwait(false))
                await Click(A, 500, token).ConfigureAwait(false);

            Log($"Hosting raid as {sav.OT} with code: {code:0000}.");
            await Task.Delay(5_000, token).ConfigureAwait(false);

            return false;
        }

        private async Task ResetGameAsync(bool airplane, CancellationToken token)
        {
            if (!ldn)
                await ResetRaidCloseGame(token).ConfigureAwait(false);
            else if (airplane)
                await ResetRaidAirplaneLDN(token).ConfigureAwait(false);
            else
                await ResetRaidCloseGameLDN(token).ConfigureAwait(false);
        }

        private async Task ResetRaidCloseGameLDN(CancellationToken token)
        {
            Log("Resetting raid by restarting the game");
            // Close out of the game
            await Click(HOME, 4_000, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false); // Closing software prompt
            Log("Closed out of the game!");

            // Open game and select profile
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            Log("Restarting the game!");

            // Switch Logo lag, skip cutscene, game load screen
            await Task.Delay(25_000, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            await Task.Delay(10_000, token).ConfigureAwait(false);

            while (!(await IsCorrectScreen(CurrentScreen_WildArea, token).ConfigureAwait(false) || await IsCorrectScreen(CurrentScreen_Overworld, token).ConfigureAwait(false)))
            {
                await Task.Delay(1000, token).ConfigureAwait(false);
            }

            Log("Back in the overworld!");
            if (Roll || FRAdd || FRBoth)
                return;

            // Reconnect to ycomm.
            await EnsureConnectedToYComm(Hub.Config, token).ConfigureAwait(false);
            Log("Reconnected to Y-Comm!");
        }

        private async Task ResetRaidAirplaneLDN(CancellationToken token)
        {
            Log("Resetting raid using Airplane Mode method");
            // Airplane mode method (only works when you connect with someone but faster)
            // Need to test if ldn_mitm crashes
            // Side menu
            await PressAndHold(HOME, 2_000, 1_000, token).ConfigureAwait(false);

            // Navigate to Airplane mode
            for (int i = 0; i < 4; i++)
                await Click(DDOWN, 1_200, token).ConfigureAwait(false);

            // Press A to turn on Airplane mode and then A again to turn it off (causes a freeze for a solid minute?)
            await Click(A, 1_200, token).ConfigureAwait(false);
            await Click(A, 1_200, token).ConfigureAwait(false);
            await Click(B, 1_200, token).ConfigureAwait(false);
            Log("Toggled Airplane Mode!");

            // Press OK on the error
            await Click(A, 1_200, token).ConfigureAwait(false);

            while (!await IsCorrectScreen(CurrentScreen_WildArea, token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
            Log("Back in the overworld!");

            // Reconnect to ycomm.
            await EnsureConnectedToYComm(Hub.Config, token).ConfigureAwait(false);
            Log("Reconnected to Y-Comm!");
        }

        private async Task ResetRaidCloseGame(CancellationToken token)
        {
            var buttons = new[]
            {
                HOME, X, A, // Close out of the game
                A, A, // Open game and select profile
                B, B, B, B, B,// Delay 20 seconds for switch logo lag
                A, B, B, // Overworld!
                Y, PLUS, // Connect to Y-Comm
                B, B, B, B // Ensure Overworld
            };
            await DaisyChainCommands(5_000, buttons, token).ConfigureAwait(false);
        }

        private int ResetCount;
        private int FriendTime;

        private async Task AutoRollDen(CancellationToken token)
        {
            for (int day = 0; day < 3; day++)
            {
                if (ResetCount == 0 || ResetCount >= 58)
                {
                    if (FriendRemoveCount != 0)
                        await FriendRemove(token).ConfigureAwait(false);

                    await Click(B, 100, token).ConfigureAwait(false);
                    await TimeMenu(token).ConfigureAwait(false);
                    await ResetTime(token).ConfigureAwait(false);
                    day = 0;
                }

                if ((FriendTime > 0 && FriendTime % 3 == 0) && (FRBoth || FRAdd))
                {
                    if ((FRBoth == FRAdd) == true)
                    {
                        Log("Enable one or the other, not both. Running FriendAdd instead.");
                        Settings.FriendCombined = false;
                        day = 0;
                        await FriendAdd(token).ConfigureAwait(false);
                        FriendTime = 0;
                    }
                    
                    if (FRBoth)
                    {
                        day = 0;
                        await FRAddRemove(token).ConfigureAwait(false);
                        FriendTime = 0;
                    }

                    if (FRAdd)
                    {
                        day = 0;
                        await FriendAdd(token).ConfigureAwait(false);
                        FriendTime = 0;
                    }
                }

                if (day == 0) //Enters den and invites others on day 1
                {
                    while (!await IsCorrectScreen(CurrentScreen_RaidParty, token).ConfigureAwait(false))
                        await Click(A, 1000, token).ConfigureAwait(false);

                    await Click(A, 4000, token).ConfigureAwait(false);
                }

                if (day < 2) //Delay until back in lobby
                {
                    while (!await IsCorrectScreen(CurrentScreen_RaidParty, token).ConfigureAwait(false))
                        await Task.Delay(1000, token).ConfigureAwait(false);
                }

                await TimeMenu(token).ConfigureAwait(false); //Goes to system time screen
                await TimeSkip(token).ConfigureAwait(false); //Skips a year
                while (!await IsCorrectScreen(CurrentScreen_RaidParty, token).ConfigureAwait(false))
                    await Task.Delay(1000, token).ConfigureAwait(false);

                await Click(B, 1500, token).ConfigureAwait(false);
                await Click(A, 1500, token).ConfigureAwait(false); //Cancel lobby

                if (day == 2) //We're on the fourth frame. Collect watts, exit lobby, return to main loop
                {
                    while (!(await IsCorrectScreen(CurrentScreen_WildArea, token).ConfigureAwait(false) || await IsCorrectScreen(CurrentScreen_Overworld, token).ConfigureAwait(false)))
                        await Task.Delay(1000, token).ConfigureAwait(false);

                    while (!await IsCorrectScreen(CurrentScreen_RaidParty, token).ConfigureAwait(false))
                        await Click(A, 1000, token).ConfigureAwait(false);

                    await Click(B, 1000, token).ConfigureAwait(false);
                    Log("Completed the rolling auto-host routine.");
                    FriendTime++;
                    return;
                }

                await Task.Delay(2000, token).ConfigureAwait(false);

                while (!await IsCorrectScreen(CurrentScreen_RaidParty, token).ConfigureAwait(false))
                    await Click(A, 1000, token).ConfigureAwait(false);
                await Click(A, 4000, token).ConfigureAwait(false); //Collect watts, invite others
            }
        }

        private async Task TimeMenu(CancellationToken token)
        {
            await Click(HOME, 1500, token).ConfigureAwait(false);
            await Click(DDOWN, 150, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 30000, 0, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await Click(DLEFT, 150, token).ConfigureAwait(false);
            await Click(A, 1000, token).ConfigureAwait(false); //Enter settings

            await SetStick(SwitchStick.LEFT, 0, -30000, 1500, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await Click(A, 500, token).ConfigureAwait(false); //Scroll to system settings

            for (int i = 0; i < 4; i++)
                await Click(DDOWN, 150, token).ConfigureAwait(false);
            await Click(A, 500, token).ConfigureAwait(false); //Scroll to date/time settings

            await Click(DDOWN, 150, token).ConfigureAwait(false);
            await Click(DDOWN, 150, token).ConfigureAwait(false);
            await Click(A, 500, token).ConfigureAwait(false); //Scroll to date/time screen
        }

        private async Task TimeSkip(CancellationToken token)
        {
            await Click(DRIGHT, 150, token).ConfigureAwait(false);
            await Click(DRIGHT, 150, token).ConfigureAwait(false);
            await Click(DUP, 150, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 30000, 0, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await Click(A, 500, token).ConfigureAwait(false);
            await Click(HOME, 1000, token).ConfigureAwait(false);
            await Click(HOME, 1500, token).ConfigureAwait(false);
            ResetCount++; //Skip one year, return back into game, increase ResetCount
        }

        private async Task ResetTime(CancellationToken token)
        {
            Log("Resetting system date...");
            await Click(DRIGHT, 150, token).ConfigureAwait(false);
            await Click(DRIGHT, 150, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, -30000, 7000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 30000, 0, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await Click(A, 500, token).ConfigureAwait(false);
            await Click(HOME, 1000, token).ConfigureAwait(false);
            await Click(HOME, 1500, token).ConfigureAwait(false); //Roll back some years, go back into game
            ResetCount = 1; 
            Log("System date reset complete.");
        }

        private async Task FRAddRemove(CancellationToken token)
        {
            await Click(B, 150, token).ConfigureAwait(false);
            Log("Removing and adding friends...");
            await Click(HOME, 1500, token).ConfigureAwait(false);
            await Click(DUP, 250, token).ConfigureAwait(false);
            await Click(A, 1500, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 30000, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 100, token).ConfigureAwait(false);
            await Click(DDOWN, 250, token).ConfigureAwait(false);
            await Click(A, 1000, token).ConfigureAwait(false); //Navigate to friend list

            await SetStick(SwitchStick.LEFT, 30000, 0, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 100, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, -30000, 10000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 100, token).ConfigureAwait(false); //Navigate to the bottom of the list

            for (int i = 0; i < 5; i++)
            {
                await Click(A, 2000, token).ConfigureAwait(false);
                await Click(DDOWN, 750, token).ConfigureAwait(false);
                await Click(A, 1500, token).ConfigureAwait(false);
                await Click(A, 1500, token).ConfigureAwait(false);
                await Click(A, 6500, token).ConfigureAwait(false);
                await Click(A, 2500, token).ConfigureAwait(false); //Select and remove user
            }

            await Click(B, 500, token).ConfigureAwait(false);
            for (int i = 0; i < 3; i++)
                await Click(DDOWN, 250, token).ConfigureAwait(false);
            await Click(A, 250, token).ConfigureAwait(false);
            await Click(A, 2000, token).ConfigureAwait(false); //Navigate to add friends

            for (int i = 0; i < 5; i++)
            {
                await Click(A, 2000, token).ConfigureAwait(false);
                await Click(A, 6000, token).ConfigureAwait(false);
                await Click(A, 2000, token).ConfigureAwait(false); //Add users
            }

            await Click(HOME, 1000, token).ConfigureAwait(false);
            await Click(HOME, 1500, token).ConfigureAwait(false); //Return to game

            Log("Removed and added 5 friends.");
            await Task.Delay(1000, token).ConfigureAwait(false);
        }

        private async Task FriendRemove(CancellationToken token)
        {
            await Click(B, 150, token).ConfigureAwait(false);
            Log("Purging friends list...");
            await Click(HOME, 1500, token).ConfigureAwait(false);
            await Click(DUP, 250, token).ConfigureAwait(false);
            await Click(A, 1500, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 30000, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await Click(DDOWN, 250, token).ConfigureAwait(false);
            await Click(A, 1000, token).ConfigureAwait(false); //Navigate to friend list

            await SetStick(SwitchStick.LEFT, 30000, 0, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 100, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, -30000, 10000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 100, token).ConfigureAwait(false); //Navigate to the bottom of the list

            for (int i = 0; i < FriendRemoveCount; i++)
            {
                await Click(A, 2000, token).ConfigureAwait(false);
                await Click(DDOWN, 750, token).ConfigureAwait(false);
                await Click(A, 1500, token).ConfigureAwait(false);
                await Click(A, 1500, token).ConfigureAwait(false);
                await Click(A, 6500, token).ConfigureAwait(false);
                await Click(A, 2500, token).ConfigureAwait(false); //Select and remove user
            }

            await Click(HOME, 1000, token).ConfigureAwait(false);
            await Click(HOME, 1500, token).ConfigureAwait(false); //Return to game

            if (FriendRemoveCount > 1)
                Log($"Purge complete. Removed {FriendRemoveCount} friends.");

            if (FriendRemoveCount == 1)
                Log($"Purge complete. Removed {FriendRemoveCount} friend.");

            FriendRemoveCount = 0;
            await Task.Delay(1000, token).ConfigureAwait(false);
        }

        private async Task FriendAdd(CancellationToken token)
        {
            await Click(B, 150, token).ConfigureAwait(false);
            Log("Adding friends...");
            await Click(HOME, 1500, token).ConfigureAwait(false);
            await Click(DUP, 250, token).ConfigureAwait(false);
            await Click(A, 1500, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, -30000, 1000, token).ConfigureAwait(false);
            await SetStick(SwitchStick.LEFT, 0, 0, 250, token).ConfigureAwait(false);
            await Click(DUP, 250, token).ConfigureAwait(false);
            await Click(A, 1000, token).ConfigureAwait(false); //Navigate to "Add Friend"
            for (int i = 0; i < 5; i++)
            {
                await Click(A, 2000, token).ConfigureAwait(false);
                await Click(A, 6000, token).ConfigureAwait(false);
                await Click(A, 2000, token).ConfigureAwait(false); //Add users
            }

            await Click(HOME, 1000, token).ConfigureAwait(false);
            await Click(HOME, 1500, token).ConfigureAwait(false); //Return to game
            await Task.Delay(1000, token).ConfigureAwait(false);

            Log("Added some friends.");
        }
    }
}