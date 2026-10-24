using System.Collections.Generic;

namespace RWDE.Graphics
{
    /// <summary>
    /// The root container, as Futile's <c>FStage</c>. <see cref="BuildBatches"/> walks the tree back to
    /// front and packs consecutive sprites that share an atlas and shader into one <see cref="FRenderBatch"/>,
    /// as Futile's <c>FRenderer</c> does with render layers. Draw order is kept: a sprite on another atlas
    /// between two sprites splits the batch.
    /// </summary>
    public sealed class FStage : FContainer
    {
        private readonly List<FRenderBatch> _batches = new List<FRenderBatch>();
        private readonly List<FRenderBatch> _pool = new List<FRenderBatch>();

        /// <summary>The batches from the last <see cref="BuildBatches"/>, back to front.</summary>
        public IReadOnlyList<FRenderBatch> Batches => _batches;

        /// <summary>Recomputes every visible node's transform and rebuilds <see cref="Batches"/>.</summary>
        public IReadOnlyList<FRenderBatch> BuildBatches()
        {
            _pool.AddRange(_batches);
            _batches.Clear();
            if (isVisible) Draw(this, null, 1f);
            return _batches;
        }

        /// <summary>The batch to append to: the last one if it matches, else a new one.</summary>
        internal FRenderBatch GetBatch(FAtlas atlas, FShader shader)
        {
            if (_batches.Count > 0)
            {
                var last = _batches[_batches.Count - 1];
                if (last.atlas == atlas && last.shader == shader) return last;
            }
            FRenderBatch batch;
            if (_pool.Count > 0)
            {
                batch = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                batch = new FRenderBatch();
            }
            batch.Reset(atlas, shader);
            _batches.Add(batch);
            return batch;
        }
    }
}
