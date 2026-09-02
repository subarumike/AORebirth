namespace AORebirth.Tools.RDBDataExtractor
{
    internal struct ExportFileCounts
    {
        internal ExportFileCounts(int written, int skipped)
        {
            this.Written = written;
            this.Skipped = skipped;
        }

        internal int Written { get; }

        internal int Skipped { get; }
    }
}
