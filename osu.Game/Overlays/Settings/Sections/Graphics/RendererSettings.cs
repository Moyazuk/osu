// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Graphics
{
    public partial class RendererSettings : SettingsSubsection
    {
        private static readonly BlindFrameSyncSession blindFrameSyncSession = new BlindFrameSyncSession();

        protected override LocalisableString Header => GraphicsSettingsStrings.RendererHeader;

        private bool automaticRendererInUse;
        private Storage exportStorage = null!;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager config, OsuConfigManager osuConfig, IDialogOverlay? dialogOverlay, OsuGame? game, GameHost host, Storage storage)
        {
            var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
            var frameSync = config.GetBindable<FrameSync>(FrameworkSetting.FrameSync);
            automaticRendererInUse = renderer.Value == RendererType.Automatic;
            blindFrameSyncSession.Attach(frameSync);
            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

            Children = new Drawable[]
            {
                new SettingsItemV2(new RendererDropdown
                {
                    Caption = GraphicsSettingsStrings.Renderer,
                    Current = renderer,
                    Items = host.GetPreferredRenderersForCurrentPlatform().Order()
                                .Where(t => t != RendererType.Vulkan),
                })
                {
                    Keywords = new[] { @"compatibility", @"directx" },
                },
                new SettingsButtonV2
                {
                    Text = @"Guess current frame limiter",
                    TooltipText = @"Submit your guess for the currently active hidden frame limiter. A new random limiter is chosen after each guess.",
                    Keywords = new[] { @"fps", @"framerate", @"frame limiter", @"blind test" },
                    Action = () => dialogOverlay?.Push(new FrameSyncGuessDialog(blindFrameSyncSession)),
                },
                new SettingsButtonV2
                {
                    Text = @"Show frame limiter statistics",
                    TooltipText = @"Open the session results dialog and export all recorded guesses to a file.",
                    Keywords = new[] { @"fps", @"framerate", @"frame limiter", @"statistics", @"results", @"export" },
                    Action = () => dialogOverlay?.Push(new FrameSyncStatisticsDialog(blindFrameSyncSession, exportStorage)),
                },
                new SettingsItemV2(new FormEnumDropdown<ExecutionMode>
                {
                    Caption = GraphicsSettingsStrings.ThreadingMode,
                    Current = config.GetBindable<ExecutionMode>(FrameworkSetting.ExecutionMode)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = GraphicsSettingsStrings.ShowFPS,
                    Current = osuConfig.GetBindable<bool>(OsuSetting.ShowFpsDisplay),
                })
                {
                    Keywords = new[] { @"framerate", @"counter" },
                },
            };

            renderer.BindValueChanged(r =>
            {
                if (r.NewValue == host.ResolvedRenderer)
                    return;

                // Need to check startup renderer for the "automatic" case, as ResolvedRenderer above will track the final resolved renderer instead.
                if (r.NewValue == RendererType.Automatic && automaticRendererInUse)
                    return;

                if (game?.RestartAppWhenExited() == true)
                {
                    game.AttemptExit();
                }
                else
                {
                    dialogOverlay?.Push(new ConfirmDialog(GraphicsSettingsStrings.ChangeRendererConfirmation, () => game?.AttemptExit(), () =>
                    {
                        renderer.Value = automaticRendererInUse ? RendererType.Automatic : host.ResolvedRenderer;
                    }));
                }
            });
        }

        private partial class RendererDropdown : FormEnumDropdown<RendererType>
        {
            private RendererType hostResolvedRenderer;
            private bool automaticRendererInUse;

            [BackgroundDependencyLoader]
            private void load(FrameworkConfigManager config, GameHost host)
            {
                var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
                automaticRendererInUse = renderer.Value == RendererType.Automatic;
                hostResolvedRenderer = host.ResolvedRenderer;
            }

            protected override LocalisableString GenerateItemText(RendererType item)
            {
                if (item == RendererType.Automatic && automaticRendererInUse)
                    return LocalisableString.Interpolate($"{base.GenerateItemText(item)} ({hostResolvedRenderer.GetDescription()})");

                return base.GenerateItemText(item);
            }
        }

        private sealed class BlindFrameSyncSession
        {
            private static readonly FrameSync[] available_frame_syncs =
            {
                FrameSync.Limit4x,
                FrameSync.OneThousand,
                FrameSync.Unlimited,
            };

            private readonly List<GuessRecord> guesses = new List<GuessRecord>();

            private Bindable<FrameSync>? frameSyncBindable;
            private FrameSync? currentFrameSync;

            public void Attach(Bindable<FrameSync> frameSyncBindable)
            {
                this.frameSyncBindable = frameSyncBindable;

                if (currentFrameSync == null)
                    chooseNextFrameSync();
                else
                    this.frameSyncBindable.Value = currentFrameSync.Value;
            }

            public void RecordGuess(FrameSync guessedFrameSync)
            {
                if (currentFrameSync == null)
                    return;

                guesses.Add(new GuessRecord(currentFrameSync.Value, guessedFrameSync));
                chooseNextFrameSync();
            }

            public string ExportGuesses(Storage storage)
            {
                string filename = $@"frame-sync-guesses-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.csv";

                using (var stream = storage.CreateFileSafely(filename))
                {
                    var text = new StringBuilder()
                               .AppendLine("guess_index,actual_frame_sync,guessed_frame_sync,correct");

                    for (int i = 0; i < guesses.Count; i++)
                    {
                        GuessRecord guess = guesses[i];
                        text.AppendLine($"{i + 1},{guess.Actual},{guess.Guessed},{guess.Actual == guess.Guessed}");
                    }

                    using var writer = new System.IO.StreamWriter(stream);
                    writer.Write(text.ToString());
                }

                return filename;
            }

            public string CreateStatisticsText()
            {
                if (guesses.Count == 0)
                    return "No guesses recorded yet.";

                int correctGuesses = guesses.Count(g => g.Actual == g.Guessed);
                return $"Guessed: {correctGuesses}/{guesses.Count}\nAccuracy: {(double)correctGuesses / guesses.Count:P1}";
            }

            private void chooseNextFrameSync()
            {
                currentFrameSync = available_frame_syncs[RNG.Next(available_frame_syncs.Length)];

                if (frameSyncBindable != null)
                    frameSyncBindable.Value = currentFrameSync.Value;
            }

            public static string GetDisplayName(FrameSync frameSync) => frameSync.GetDescription();

            private readonly record struct GuessRecord(FrameSync Actual, FrameSync Guessed);
        }

        private partial class FrameSyncGuessDialog : PopupDialog
        {
            public FrameSyncGuessDialog(BlindFrameSyncSession session)
            {
                HeaderText = @"Which frame limiter do you think you had?";
                BodyText = @"Pick the limiter you think is currently active. Your guess is stored, then the game switches to a new random limiter.";
                Icon = FontAwesome.Solid.QuestionCircle;
                Buttons = new PopupDialogButton[]
                {
                    createGuessButton(session, FrameSync.Limit4x),
                    createGuessButton(session, FrameSync.OneThousand),
                    createGuessButton(session, FrameSync.Unlimited),
                    new PopupDialogCancelButton
                    {
                        Text = @"Cancel",
                    },
                };
            }

            private static PopupDialogButton createGuessButton(BlindFrameSyncSession session, FrameSync guessedFrameSync) =>
                new PopupDialogOkButton
                {
                    Text = BlindFrameSyncSession.GetDisplayName(guessedFrameSync),
                    Action = () => session.RecordGuess(guessedFrameSync),
                };
        }

        private partial class FrameSyncStatisticsDialog : PopupDialog
        {
            public FrameSyncStatisticsDialog(BlindFrameSyncSession session, Storage exportStorage)
            {
                HeaderText = @"Frame limiter statistics";
                BodyText = $"{session.CreateStatisticsText()}\n\nExport this session's recorded guesses to a CSV file.";
                Icon = FontAwesome.Solid.ChartBar;
                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = @"Export guesses",
                        Action = () => exportStorage.PresentFileExternally(session.ExportGuesses(exportStorage)),
                    },
                    new PopupDialogOkButton
                    {
                        Text = @"Close",
                    },
                };
            }
        }
    }
}
