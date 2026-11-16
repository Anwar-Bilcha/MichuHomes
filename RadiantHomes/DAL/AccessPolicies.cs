namespace RadiantHomes.DAL
{
    /// <summary>Authorization policy names (registered and applied to page folders in Program.cs).</summary>
    public static class AccessPolicies
    {
        /// <summary>Platform administrators ("Michu Admin").</summary>
        public const string MichuAdminOnly = "MichuAdminOnly";

        /// <summary>Platform or real-estate company administrators.</summary>
        public const string CompanyAdmin = "CompanyAdmin";
    }
}
