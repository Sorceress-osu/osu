


// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Screens.Play.HUD.JudgementCounter
{
    public partial class JudgementTally : CompositeDrawable
    {
        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; } = null!;

        public List<JudgementCounterInfo> Results = new List<JudgementCounterInfo>();

        [BackgroundDependencyLoader]
        private void load(IBindable<WorkingBeatmap> working)
        {
            foreach (var result in working.Value.BeatmapInfo.Ruleset.CreateInstance().GetHitResults())
            {
                Results.Add(new JudgementCounterInfo
                {
                    ResultInfo = (result.result, result.displayName),
                    ResultCount = new BindableInt()
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scoreProcessor.NewJudgement += judgement =>
            {
                foreach (JudgementCounterInfo result in Results.Where(result => result.ResultInfo.Type == judgement.Type))
                {
                    result.ResultCount.Value++;
                }
            };
            scoreProcessor.JudgementReverted += judgement =>
            {
                foreach (JudgementCounterInfo result in Results.Where(result => result.ResultInfo.Type == judgement.Type))
                {
                    result.ResultCount.Value--;
                }
            };
        }
    }
}
