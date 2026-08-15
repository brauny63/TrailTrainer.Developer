namespace TrailTrainer.Developer.Core;

public interface IAutomaticResumeSchedulingDecision
{
    AutomaticResumeSchedulingDecision Decide(AutomaticResumeBatchRunResult batchRun);
}
