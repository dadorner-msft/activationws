using ActivationWs.Data;
using ActivationWs.Exceptions;
using ActivationWs.Models;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace ActivationWs.Services
{
    public class ActivationProcessor {
        private readonly ILogger<ActivationProcessor> _logger;
        private readonly ActivationDbContext _context;
        private readonly ActivationService _activationService;

        private static readonly Regex hostNameRegex = new Regex(@"^(?=.{1,253}$)(?:(?!-)[A-Za-z0-9-]{1,63}(?<!-)\.?)+$", RegexOptions.Compiled);
        private static readonly Regex installationIDRegex = new Regex(@"^\d{63}$", RegexOptions.Compiled);
        private static readonly Regex extendedProductIDRegex = new Regex(@"^\d{5}-\d{5}-\d{3}-\d{6}-\d{2}-\d{4}-\d+\.\d{4}-\d{7}$", RegexOptions.Compiled);

        public ActivationProcessor(ILogger<ActivationProcessor> logger,
                                   ActivationDbContext context,
                                   ActivationService activationService) {
            _logger = logger;
            _context = context;
            _activationService = activationService;
        }

        public async Task<string> GetConfirmationIDAsync(string hostName, string installationID, string extendedProductID) {
            if (!hostNameRegex.IsMatch(hostName)) {
                _logger.LogError("The format of the hostname ({0}) is invalid.", hostName);
                throw new ArgumentException("The format of the hostname is invalid.");
            }

            if (!installationIDRegex.IsMatch(installationID)) {
                _logger.LogError("The format of the Installation ID ({0}) is invalid.", installationID);
                throw new ArgumentException("The format of the Installation ID is invalid.");
            }

            if (!extendedProductIDRegex.IsMatch(extendedProductID)) {
                _logger.LogError("The format of the Extended Product ID ({0}) is invalid.", extendedProductID);
                throw new ArgumentException("The format of the Extended Product ID is invalid.");
            }

            try {
                var existingRecord = await _context.ActivationRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.InstallationID == installationID && r.ExtendedProductID == extendedProductID);

                if (existingRecord != null) {
                    _logger.LogInformation("The Confirmation ID has been retrieved from the database.");
                    return existingRecord.ConfirmationID;
                }
            }
            catch (DbException dbEx) {
                _logger.LogWarning(dbEx.Message, "Failed to retrieve the Confirmation ID from the database.");
            }

            string result;
            try {
                _logger.LogInformation("About to acquire the Confirmation ID from the Microsoft Activation Service...");
                result = await _activationService.CallWebServiceAsync(1, installationID, extendedProductID);

                try {
                    var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Hostname == hostName);
                    if (machine == null) {
                        machine = new Machine { Hostname = hostName };
                        await _context.Machines.AddAsync(machine);
                        await _context.SaveChangesAsync();
                    }

                    var newRecord = new ActivationRecord {
                        MachineId = machine.Id,
                        InstallationID = installationID,
                        ExtendedProductID = extendedProductID,
                        ConfirmationID = result,
                        LicenseAcquisitionDate = DateTime.UtcNow
                    };

                    await _context.ActivationRecords.AddAsync(newRecord);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "A new record has been added to the database: Hostname={0}, InstallationID={1}, ExtendedProductID={2}, ConfirmationID={3}",
                        hostName, installationID, extendedProductID, result);
                }
                catch (Exception dbEx) {
                    _logger.LogWarning(dbEx, "Failed to save the new ActivationRecord to the database.");
                }

            } catch (HttpRequestException httpEx) {
                _logger.LogError(httpEx, "HTTP request to the Microsoft Activation Service failed.");
                throw;
            } catch (BasException basEx) {
                _logger.LogError(basEx, "The Microsoft Activation Service reported an error:");
                throw;
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to acquire the Confirmation ID from the Microsoft Activation Service.");
                throw;
            }

            return result;
        }

        public async Task<string> GetRemainingActivationCountAsync(string extendedProductID) {
            if (!extendedProductIDRegex.IsMatch(extendedProductID)) {
                _logger.LogError("The format of the Extended Product ID ({0}) is invalid.", extendedProductID);
                throw new ArgumentException("The format of the Extended Product ID is invalid.");
            }

            try {
                var result = await _activationService.CallWebServiceAsync(2, "", extendedProductID);
                _logger.LogInformation("The remaining activation count is: {0}.", result);
                return result;

            } catch (HttpRequestException httpEx) {
                _logger.LogError(httpEx, "HTTP request to the Microsoft Activation Service failed.");
                throw;
            } catch (BasException basEx) {
                _logger.LogError(basEx, "The Microsoft Activation Service reported an error:");
                throw;
            } catch (Exception ex) {
                _logger.LogError(ex, "The remaining activation count could not be retrieved.");
                throw;
            }
        }
    }
}