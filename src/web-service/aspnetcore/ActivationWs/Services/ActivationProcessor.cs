using ActivationWs.Data;
using ActivationWs.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace ActivationWs.Services
{
    public class ActivationProcessor {
        private readonly ILogger<ActivationProcessor> _logger;
        private readonly ActivationDbContext _context;

        private static readonly Regex hostNameRegex = new Regex(@"^(?=.{1,253}$)(?:(?!-)[A-Za-z0-9-]{1,63}(?<!-)\.?)+$", RegexOptions.Compiled);
        private static readonly Regex installationIdRegex = new Regex(@"^\d{63}$", RegexOptions.Compiled);
        private static readonly Regex extendedProductIdRegex = new Regex(@"^\d{5}-\d{5}-\d{3}-\d{6}-\d{2}-\d{4}-\d{4}\.\d{4}-\d{7}$", RegexOptions.Compiled);

        public ActivationProcessor(ILogger<ActivationProcessor> logger, ActivationDbContext context) {
            _logger = logger;
            _context = context;
        }

        public async Task<(string confirmationId, bool cached)> GetConfirmationIdAsync(string hostName, string installationId, string extendedProductId) {
            if (!hostNameRegex.IsMatch(hostName)) {
                _logger.LogError("The format of the hostname ({0}) is invalid.", hostName);
                throw new ArgumentException("The format of the hostname is invalid.");
            }

            if (!installationIdRegex.IsMatch(installationId)) {
                _logger.LogError("The format of the Installation ID ({0}) is invalid.", installationId);
                throw new ArgumentException("The format of the Installation ID is invalid.");
            }

            if (!extendedProductIdRegex.IsMatch(extendedProductId)) {
                _logger.LogError("The format of the Extended Product ID ({0}) is invalid.", extendedProductId);
                throw new ArgumentException("The format of the Extended Product ID is invalid.");
            }

            // Try to read from the database, but continue if it fails
            try {
                var existingRecord = await _context.ActivationRecords
                    .FirstOrDefaultAsync(r => r.InstallationId == installationId && r.ExtendedProductId == extendedProductId);

                if (existingRecord != null) {
                    existingRecord.LastRequestDate = DateTime.UtcNow;

                    if (!string.Equals(existingRecord.Hostname, hostName, StringComparison.Ordinal)) {
                        _logger.LogInformation("Hostname updated for InstallationId={0}, ExtendedProductId={1}: {2} -> {3}", installationId, extendedProductId, existingRecord.Hostname, hostName);
                        existingRecord.Hostname = hostName;
                    }

                    try {
                        await _context.SaveChangesAsync();

                    } catch (DbException dbEx) {
                        _logger.LogWarning(dbEx.Message, "Unable to update the ActivationRecord in the database.");
                    }

                    _logger.LogInformation("The Confirmation ID has been retrieved from the database.");
                    return (existingRecord.ConfirmationId, true);
                }
            }
            catch (DbException dbEx) {
                _logger.LogWarning(dbEx.Message, "Unable to retrieve the Confirmation ID from the database.");
            }

            // If not found in the database or DB query failed, call the web service
            string result;
            try {
                _logger.LogInformation("About to retrieve the Confirmation ID from the Microsoft Batch Activation Service.");
                result = await ActivationService.CallWebServiceAsync(1, installationId, extendedProductId);

            } catch (HttpRequestException httpEx) {
                _logger.LogError(httpEx, "HTTP request to the Microsoft Batch Activation Service failed.");
                throw new Exception("Unable to retrieve the Confirmation ID due to a network or service error.", httpEx);

            } catch (Exception ex) {
                _logger.LogError(ex, "An unexpected error occurred while retrieving the Confirmation ID from the Microsoft Batch Activation Service.");
                throw new Exception("An internal error occurred while processing the request.", ex);
            }

            // Try to save the Confirmation ID to the database
            try {
                var newRecord = new ActivationRecord
                {
                    Hostname = hostName,
                    InstallationId = installationId,
                    ExtendedProductId = extendedProductId,
                    ConfirmationId = result,
                    ActivationDate = DateTime.UtcNow,
                    LastRequestDate = DateTime.UtcNow,
                    LicenseStatus = ""
                };

                _context.ActivationRecords.Add(newRecord);
                await _context.SaveChangesAsync();

                _logger.LogInformation("A new record has been added to the database: Hostname={0}, InstallationId={1}, ExtendedProductId={2}, ConfirmationId={3}", hostName, installationId, extendedProductId, result);

            } catch (Exception dbEx) {
                // Do not throw, just log and continue
                _logger.LogWarning(dbEx, "Unable to save the new ActivationRecord to the database.");
            }

            return (result, false);
        }

        public async Task<string> GetRemainingActivationCountAsync(string extendedProductId) {
            if (!extendedProductIdRegex.IsMatch(extendedProductId)) {
                _logger.LogError("The format of the Extended Product ID ({0}) is invalid.", extendedProductId);
                throw new ArgumentException("The format of the Extended Product ID is invalid.");
            }

            try {
                var result = await ActivationService.CallWebServiceAsync(2, "", extendedProductId);
                _logger.LogInformation("The remaining activation count is: {0}.", result);
                return result;

            } catch (HttpRequestException httpEx) {
                _logger.LogError(httpEx, "HTTP request to the Microsoft Batch Activation Service failed.");
                throw new Exception(httpEx.Message);

            } catch (Exception ex) {
                _logger.LogError(ex, "The remaining activation count could not be retrieved.");
                throw new Exception(ex.Message);
            }
        }
    }
}