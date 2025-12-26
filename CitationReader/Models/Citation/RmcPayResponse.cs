using System.Text.Json.Serialization;

namespace CitationReader.Models.Citation;

public class RmcPayResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("data")]
    public List<ViolationData> Data { get; set; } = new();

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    public class ViolationData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("violation_number")]
        public string ViolationNumber { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("date_utc")]
        public string DateUtc { get; set; } = string.Empty;

        [JsonPropertyName("amountincents")]
        public string AmountInCents { get; set; } = string.Empty;

        [JsonPropertyName("lpn")]
        public string Lpn { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("paid")]
        public string Paid { get; set; } = string.Empty;

        [JsonPropertyName("amountpaid")]
        public string AmountPaid { get; set; } = string.Empty;

        [JsonPropertyName("invoiced")]
        public string Invoiced { get; set; } = string.Empty;

        [JsonPropertyName("rmcpay_status")]
        public string RmcPayStatus { get; set; } = string.Empty;

        [JsonPropertyName("voidedby_name")]
        public string? VoidedByName { get; set; }

        [JsonPropertyName("canappeal")]
        public string CanAppeal { get; set; } = string.Empty;

        [JsonPropertyName("canhearing")]
        public string CanHearing { get; set; } = string.Empty;

        [JsonPropertyName("violationappeal_status")]
        public string ViolationAppealStatus { get; set; } = string.Empty;

        [JsonPropertyName("state_id")]
        public string StateId { get; set; } = string.Empty;

        [JsonPropertyName("operator_id")]
        public string OperatorId { get; set; } = string.Empty;

        [JsonPropertyName("enforcement_provider")]
        public string EnforcementProvider { get; set; } = string.Empty;

        [JsonPropertyName("enforcement_provider_class")]
        public string EnforcementProviderClass { get; set; } = string.Empty;

        [JsonPropertyName("enforcement_partner_id")]
        public string? EnforcementPartnerId { get; set; }

        [JsonPropertyName("officer_name")]
        public string? OfficerName { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("officer_number")]
        public string? OfficerNumber { get; set; }

        [JsonPropertyName("officer_notes")]
        public string? OfficerNotes { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("referencenumber")]
        public string? ReferenceNumber { get; set; }

        [JsonPropertyName("paymenttypedescription")]
        public string PaymentTypeDescription { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("isviolation")]
        public string IsViolation { get; set; } = string.Empty;

        [JsonPropertyName("internalcitationnumber")]
        public string InternalCitationNumber { get; set; } = string.Empty;

        [JsonPropertyName("st")]
        public string St { get; set; } = string.Empty;

        [JsonPropertyName("appealstatus_description")]
        public string? AppealStatusDescription { get; set; }

        [JsonPropertyName("response_on_appeal")]
        public string? ResponseOnAppeal { get; set; }

        [JsonPropertyName("appeal_id")]
        public string? AppealId { get; set; }

        [JsonPropertyName("appeal_comment")]
        public string? AppealComment { get; set; }

        [JsonPropertyName("appeal_description")]
        public string? AppealDescription { get; set; }

        [JsonPropertyName("operator_name")]
        public string OperatorName { get; set; } = string.Empty;

        [JsonPropertyName("operator_display_name")]
        public string OperatorDisplayName { get; set; } = string.Empty;

        [JsonPropertyName("vin")]
        public string? Vin { get; set; }

        [JsonPropertyName("vmodel")]
        public string? VModel { get; set; }

        [JsonPropertyName("vmake")]
        public string? VMake { get; set; }

        [JsonPropertyName("vcolor")]
        public string? VColor { get; set; }

        [JsonPropertyName("vtype")]
        public string VType { get; set; } = string.Empty;

        [JsonPropertyName("vtype_name")]
        public string VTypeName { get; set; } = string.Empty;

        [JsonPropertyName("countrycode_id")]
        public string CountryCodeId { get; set; } = string.Empty;

        [JsonPropertyName("make_id")]
        public string MakeId { get; set; } = string.Empty;

        [JsonPropertyName("model_id")]
        public string ModelId { get; set; } = string.Empty;

        [JsonPropertyName("color_id")]
        public string ColorId { get; set; } = string.Empty;

        [JsonPropertyName("vehiclestate")]
        public string VehicleState { get; set; } = string.Empty;

        [JsonPropertyName("vehiclecountry")]
        public string VehicleCountry { get; set; } = string.Empty;

        [JsonPropertyName("state_name")]
        public string StateName { get; set; } = string.Empty;

        [JsonPropertyName("zone")]
        public string Zone { get; set; } = string.Empty;

        [JsonPropertyName("zonenumber")]
        public string ZoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("spacenumber")]
        public string SpaceNumber { get; set; } = string.Empty;

        [JsonPropertyName("due")]
        public string Due { get; set; } = string.Empty;

        [JsonPropertyName("fee")]
        public string Fee { get; set; } = string.Empty;

        [JsonPropertyName("penaltyfee")]
        public string PenaltyFee { get; set; } = string.Empty;

        [JsonPropertyName("convfee")]
        public string ConvFee { get; set; } = string.Empty;

        [JsonPropertyName("trfee")]
        public string TrFee { get; set; } = string.Empty;

        [JsonPropertyName("violationtype_id")]
        public string ViolationTypeId { get; set; } = string.Empty;

        [JsonPropertyName("allow_appeal_after_paid")]
        public string AllowAppealAfterPaid { get; set; } = string.Empty;

        [JsonPropertyName("settlementdate")]
        public string SettlementDate { get; set; } = string.Empty;

        [JsonPropertyName("paid_date")]
        public string PaidDate { get; set; } = string.Empty;

        [JsonPropertyName("paid_date_utc")]
        public string PaidDateUtc { get; set; } = string.Empty;

        [JsonPropertyName("days_outstanding")]
        public string DaysOutstanding { get; set; } = string.Empty;

        [JsonPropertyName("canappealdays")]
        public string CanAppealDays { get; set; } = string.Empty;

        [JsonPropertyName("daysunpaid")]
        public string DaysUnpaid { get; set; } = string.Empty;

        [JsonPropertyName("maxappeals_over")]
        public string MaxAppealsOver { get; set; } = string.Empty;

        [JsonPropertyName("past_appeal_date")]
        public string PastAppealDate { get; set; } = string.Empty;

        [JsonPropertyName("zone_id")]
        public string ZoneId { get; set; } = string.Empty;

        [JsonPropertyName("voidedby")]
        public string VoidedBy { get; set; } = string.Empty;

        [JsonPropertyName("void_release_date")]
        public string? VoidReleaseDate { get; set; }

        [JsonPropertyName("adjustments")]
        public string Adjustments { get; set; } = string.Empty;

        [JsonPropertyName("date_sent_to_collections")]
        public string? DateSentToCollections { get; set; }

        [JsonPropertyName("letterssent")]
        public string LettersSent { get; set; } = string.Empty;

        [JsonPropertyName("latest_violation_letteragency_id")]
        public string? LatestViolationLetterAgencyId { get; set; }

        [JsonPropertyName("paidbycollections")]
        public string PaidByCollections { get; set; } = string.Empty;

        [JsonPropertyName("appealleveloperatorroleid")]
        public string? AppealLevelOperatorRoleId { get; set; }

        [JsonPropertyName("lpn_vin")]
        public string LpnVin { get; set; } = string.Empty;

        [JsonPropertyName("has_notes")]
        public string HasNotes { get; set; } = string.Empty;

        [JsonPropertyName("is_legacy")]
        public string IsLegacy { get; set; } = string.Empty;

        [JsonPropertyName("userdef1")]
        public string? UserDef1 { get; set; }

        [JsonPropertyName("userdef2")]
        public string? UserDef2 { get; set; }

        [JsonPropertyName("userdef3")]
        public string? UserDef3 { get; set; }

        [JsonPropertyName("userdef4")]
        public string? UserDef4 { get; set; }

        [JsonPropertyName("userdef5")]
        public string? UserDef5 { get; set; }

        [JsonPropertyName("userdef6")]
        public string? UserDef6 { get; set; }

        [JsonPropertyName("userdef7")]
        public string? UserDef7 { get; set; }

        [JsonPropertyName("userdef8")]
        public string? UserDef8 { get; set; }

        [JsonPropertyName("userdef9")]
        public string? UserDef9 { get; set; }

        [JsonPropertyName("userdef10")]
        public string? UserDef10 { get; set; }

        [JsonPropertyName("can_escalate")]
        public string CanEscalate { get; set; } = string.Empty;

        [JsonPropertyName("fleetname")]
        public string? FleetName { get; set; }

        [JsonPropertyName("fleet_id")]
        public string FleetId { get; set; } = string.Empty;

        [JsonPropertyName("rental_agency")]
        public string? RentalAgency { get; set; }

        [JsonPropertyName("late_invoice_amount")]
        public string LateInvoiceAmount { get; set; } = string.Empty;

        [JsonPropertyName("invoicenum")]
        public string? InvoiceNum { get; set; }

        [JsonPropertyName("operator_has_whitelabel")]
        public string OperatorHasWhiteLabel { get; set; } = string.Empty;

        [JsonPropertyName("userdef1_label")]
        public string? UserDef1Label { get; set; }

        [JsonPropertyName("userdef2_label")]
        public string? UserDef2Label { get; set; }

        [JsonPropertyName("userdef3_label")]
        public string? UserDef3Label { get; set; }

        [JsonPropertyName("userdef4_label")]
        public string? UserDef4Label { get; set; }

        [JsonPropertyName("userdef5_label")]
        public string? UserDef5Label { get; set; }

        [JsonPropertyName("userdef6_label")]
        public string? UserDef6Label { get; set; }

        [JsonPropertyName("userdef7_label")]
        public string? UserDef7Label { get; set; }

        [JsonPropertyName("userdef8_label")]
        public string? UserDef8Label { get; set; }

        [JsonPropertyName("userdef9_label")]
        public string? UserDef9Label { get; set; }

        [JsonPropertyName("userdef10_label")]
        public string? UserDef10Label { get; set; }

        [JsonPropertyName("plate_type_id")]
        public string PlateTypeId { get; set; } = string.Empty;

        [JsonPropertyName("plate_type_name")]
        public string PlateTypeName { get; set; } = string.Empty;

        [JsonPropertyName("hold_status")]
        public string HoldStatus { get; set; } = string.Empty;

        [JsonPropertyName("hold_lift_date")]
        public string? HoldLiftDate { get; set; }

        [JsonPropertyName("hold_sent_date")]
        public string? HoldSentDate { get; set; }

        [JsonPropertyName("notes")]
        public List<Note> Notes { get; set; } = new();

        [JsonPropertyName("marks")]
        public List<object> Marks { get; set; } = new();

        [JsonPropertyName("operator_account_id")]
        public string OperatorAccountId { get; set; } = string.Empty;

        [JsonPropertyName("elapsed_time")]
        public string? ElapsedTime { get; set; }

        [JsonPropertyName("void_status")]
        public string VoidStatus { get; set; } = string.Empty;

        [JsonPropertyName("is_hearing_request")]
        public string IsHearingRequest { get; set; } = string.Empty;

        [JsonPropertyName("disable_credit_card_fee")]
        public string DisableCreditCardFee { get; set; } = string.Empty;

        [JsonPropertyName("payment_plan_id")]
        public string? PaymentPlanId { get; set; }

        [JsonPropertyName("is_violator_liable")]
        public string? IsViolatorLiable { get; set; }

        [JsonPropertyName("statutecode")]
        public string? Statutecode { get; set; }

        [JsonPropertyName("visible_rmc")]
        public string VisibleRmc { get; set; } = string.Empty;

        [JsonPropertyName("immobilization_device_number")]
        public string? ImmobilizationDeviceNumber { get; set; }

        [JsonPropertyName("immobilization_status")]
        public string? ImmobilizationStatus { get; set; }

        [JsonPropertyName("immobilization_id")]
        public string? ImmobilizationId { get; set; }

        [JsonPropertyName("boot")]
        public string Boot { get; set; } = string.Empty;

        [JsonPropertyName("tow")]
        public string Tow { get; set; } = string.Empty;

        [JsonPropertyName("scofflaw_eligible")]
        public string ScofflawEligible { get; set; } = string.Empty;

        [JsonPropertyName("special_text")]
        public string? SpecialText { get; set; }

        [JsonPropertyName("adjustment_items")]
        public List<object> AdjustmentItems { get; set; } = new();

        [JsonPropertyName("infraction_types")]
        public string? InfractionTypes { get; set; }

        [JsonPropertyName("infractions_amount")]
        public string? InfractionsAmount { get; set; }

        [JsonPropertyName("is_fleet_v2")]
        public string IsFleetV2 { get; set; } = string.Empty;

        [JsonPropertyName("is_fleet_v2_invoiced")]
        public string IsFleetV2Invoiced { get; set; } = string.Empty;

        [JsonPropertyName("is_fleet_v1")]
        public string IsFleetV1 { get; set; } = string.Empty;

        [JsonPropertyName("data_signature")]
        public string DataSignature { get; set; } = string.Empty;
        
        public class Note
        {
            [JsonPropertyName("note")]
            public string NoteText { get; set; } = string.Empty;
        }
    }
}