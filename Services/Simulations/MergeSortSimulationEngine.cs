using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Merge Sort (top-down recursive).
/// </summary>
public class MergeSortSimulationEngine : IAlgorithmSimulationEngine
{
    private const string AlgorithmDisplayName = "Merge Sort";

    // ── Pseudocode line numbers ──────────────────────────────────────────────
    private static class Line
    {
        public const int Start          = 1;
        public const int RecursiveCall  = 2;
        public const int BaseCase       = 3;
        public const int Split          = 4;
        public const int SortLeft       = 5;
        public const int SortRight      = 6;
        public const int MergeStart     = 7;
        public const int Compare        = 8;
        public const int Place          = 9;
        public const int MergeComplete  = 10;
        public const int Return         = 11;
        public const int Complete       = 12;
    }

    // ── Internal context shared across all recursive frames ─────────────────
    private sealed class MergeContext
    {
        public List<SimulationStep> Steps { get; } = [];
        public int StepNumber { get; set; } = 1;
        public List<RecursionFrameModel> Stack { get; } = [];
        public int NextFrameId { get; set; } = 1;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public bool CanHandle(string algorithm)
    {
        if (string.IsNullOrWhiteSpace(algorithm)) return false;
        var normalized = algorithm.Trim().ToLowerInvariant();
        return normalized is "merge_sort" or "merge-sort";
    }

    public SimulationResponse Run(int[] array, int? targetValue = null)
    {
        var ctx = new MergeContext();
        var work = array.ToArray();

        AddStep(ctx, work, [], Line.Start, "start",
            Meta("start", 0, 0, work.Length - 1));

        if (work.Length > 1)
        {
            MergeSortRecursive(work, 0, work.Length - 1, ctx, 0);
        }
        else
        {
            // Single element or empty — emit a base-case step
            AddStep(ctx, work, work.Length == 1 ? [0] : [], Line.BaseCase, "base_case",
                Meta("base_case", 0, 0, Math.Max(0, work.Length - 1)));
        }

        AddStep(ctx, work, [], Line.Complete, "complete",
            Meta("complete", 0, 0, work.Length - 1));

        return new SimulationResponse
        {
            AlgorithmName = AlgorithmDisplayName,
            Steps = ctx.Steps,
            TotalSteps = ctx.Steps.Count
        };
    }

    // ── Recursive sort ───────────────────────────────────────────────────────

    private static void MergeSortRecursive(int[] array, int left, int right, MergeContext ctx, int depth)
    {
        // Push call frame
        var frame = new RecursionFrameModel
        {
            Id        = ctx.NextFrameId++,
            FunctionName = "mergeSort",
            Depth     = depth,
            State     = "call",
            LeftIndex = left,
            RightIndex = right
        };
        ctx.Stack.Add(frame);

        /* AddStep(ctx, array, [left, right], Line.RecursiveCall, "recursive_call",
            Meta("call", depth, left, right)); */

        // Base case — sub-array of size 1
        if (left >= right)
        {
            frame.State = "return";
            frame.ReturnValue = "base_case";

            /* AddStep(ctx, array, [left], Line.BaseCase, "base_case",
                Meta("base_case", depth, left, right)); */

            ctx.Stack.RemoveAt(ctx.Stack.Count - 1);
            return;
        }

        // Split
        var mid = left + (right - left) / 2;

        AddStep(ctx, array, Enumerable.Range(left, right - left + 1).ToArray(), Line.Split, "split",
            Meta("split", depth, left, right, mid: mid));

        // Recurse left
        /* AddStep(ctx, array, Enumerable.Range(left, mid - left + 1).ToArray(), Line.SortLeft, "sort_left_start",
            Meta("sort_left_start", depth, left, mid)); */

        MergeSortRecursive(array, left, mid, ctx, depth + 1);

        /* AddStep(ctx, array, Enumerable.Range(left, mid - left + 1).ToArray(), Line.SortLeft, "sort_left_complete",
            Meta("sort_left_complete", depth, left, mid)); */

        // Recurse right
        /* AddStep(ctx, array, Enumerable.Range(mid + 1, right - mid).ToArray(), Line.SortRight, "sort_right_start",
            Meta("sort_right_start", depth, mid + 1, right)); */

        MergeSortRecursive(array, mid + 1, right, ctx, depth + 1);

        /* AddStep(ctx, array, Enumerable.Range(mid + 1, right - mid).ToArray(), Line.SortRight, "sort_right_complete",
            Meta("sort_right_complete", depth, mid + 1, right)); */

        // Merge the two sorted halves
        Merge(array, left, mid, right, ctx, depth);

        frame.State = "return";
        frame.ReturnValue = $"sorted[{left}..{right}]";

        /* AddStep(ctx, array, Enumerable.Range(left, right - left + 1).ToArray(), Line.Return, "return",
            Meta("return", depth, left, right)); */

        ctx.Stack.RemoveAt(ctx.Stack.Count - 1);
    }

    // ── Merge two adjacent sorted halves in place ────────────────────────────

    private static void Merge(int[] array, int left, int mid, int right, MergeContext ctx, int depth)
    {
        // Copy both halves into a temporary buffer
        var leftPart  = array[left..(mid + 1)];
        var rightPart = array[(mid + 1)..(right + 1)];
        var buffer    = leftPart.Concat(rightPart).ToArray();

        /* AddStep(ctx, array, Enumerable.Range(left, right - left + 1).ToArray(), Line.MergeStart, "merge_start",
            Meta("merge_start", depth, left, right, mid: mid, mergeBuffer: buffer)); */

        var i = 0;               // pointer into left half
        var j = 0;               // pointer into right half
        var k = left;            // write position in main array

        // Interleave — emit a compare step then a place step for each win
        while (i < leftPart.Length && j < rightPart.Length)
        {
            var leftCandidate  = left  + i;
            var rightCandidate = mid   + 1 + j;

            AddStep(ctx, array, [leftCandidate, rightCandidate], Line.Compare, "compare",
                Meta("compare", depth, left, right, mid: mid, mergeBuffer: [.. buffer]));

            if (leftPart[i] <= rightPart[j])
            {
                array[k] = leftPart[i++];
            }
            else
            {
                array[k] = rightPart[j++];
            }

            buffer = leftPart[i..].Concat(rightPart[j..]).ToArray();

            AddStep(ctx, array, [k], Line.Place, "place",
                Meta("place", depth, left, right, mid: mid, mergeBuffer: [.. buffer], placeIndex: k));

            k++;
        }

        // Drain remaining left elements
        while (i < leftPart.Length)
        {
            array[k] = leftPart[i++];

            buffer = leftPart[i..].Concat(rightPart[j..]).ToArray();

            AddStep(ctx, array, [k], Line.Place, "place",
                Meta("place", depth, left, right, mid: mid, mergeBuffer: [.. buffer], placeIndex: k));

            k++;
        }

        // Drain remaining right elements
        while (j < rightPart.Length)
        {
            array[k] = rightPart[j++];

            buffer = leftPart[i..].Concat(rightPart[j..]).ToArray();

            AddStep(ctx, array, [k], Line.Place, "place",
                Meta("place", depth, left, right, mid: mid, mergeBuffer: [.. buffer], placeIndex: k));

            k++;
        }

        /* AddStep(ctx, array, Enumerable.Range(left, right - left + 1).ToArray(), Line.MergeComplete, "merge_complete",
            Meta("merge_complete", depth, left, right, mid: mid)); */
    }

    // ── Helper builders ──────────────────────────────────────────────────────

    private static MergeSortStepModel Meta(
        string type,
        int depth,
        int left,
        int right,
        int? mid         = null,
        int[]? mergeBuffer = null,
        int? placeIndex  = null)
    {
        return new MergeSortStepModel
        {
            Type           = type,
            Left           = left,
            Right          = right,
            Mid            = mid,
            RecursionDepth = depth,
            MergeBuffer    = mergeBuffer,
            PlaceIndex     = placeIndex
        };
    }

    private static RecursionStepModel BuildRecursion(MergeContext ctx, string state)
    {
        var current = ctx.Stack.Count > 0 ? ctx.Stack[^1] : null;
        return new RecursionStepModel
        {
            State          = state,
            Depth          = current?.Depth ?? 0,
            CurrentFrameId = current?.Id,
            Stack          = [.. ctx.Stack]
        };
    }

    private static void AddStep(
        MergeContext ctx,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
        MergeSortStepModel mergeSort)
    {
        ctx.Steps.Add(new SimulationStep
        {
            StepNumber    = ctx.StepNumber++,
            ArrayState    = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber    = lineNumber,
            ActionLabel   = actionLabel,
            MergeSort     = mergeSort,
            Recursion     = BuildRecursion(ctx, actionLabel)
        });
    }
}
