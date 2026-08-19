/**
 * Open in Sheets - optional self-hosted backend.
 *
 * You do not need this. The app signs in with Google out of the box. Use this only
 * if you would rather run the whole thing under your own Google account and not use
 * the app's OAuth client at all.
 *
 * Setup (once):
 *   1. script.google.com -> New project -> paste this file over Code.gs
 *   2. Services (+) -> Drive API -> Add
 *   3. Set SECRET below to a long random string of your own (24+ characters)
 *   4. Deploy -> New deployment -> Web app
 *        Execute as:      Me
 *        Who has access:  Anyone
 *   5. In Open in Sheets: Advanced settings -> "Use my own Apps Script",
 *      paste the /exec address and the same secret
 *
 * "Anyone" means anyone who knows the address *and* the secret. Requests without a
 * matching secret are rejected before any Drive call happens, and this script will
 * only ever write to files inside its own folder.
 */

var SECRET = 'REPLACE_ME';
var FOLDER_NAME = 'CSV Quick Open';

function doGet() {
  return reply({ ok: true, service: 'open-in-sheets' });
}

function doPost(e) {
  try {
    // The length check alone catches the unset placeholder. Deliberately no literal
    // comparison here: the placeholder must appear exactly once in this file, or a
    // find-and-replace of it also rewrites the guard and breaks doPost.
    if (!SECRET || SECRET.length < 24) {
      throw new Error('SECRET is not set in Code.gs - put a long random string there.');
    }
    var req = JSON.parse(e.postData.contents);
    if (req.secret !== SECRET) {
      return reply({ error: 'Unauthorized - the secret in the app does not match Code.gs.' });
    }

    var name = String(req.name || 'data.csv').replace(/\.csv$/i, '');
    var blob = Utilities.newBlob(Utilities.base64Decode(req.data), 'text/csv', name + '.csv');

    // Reuse the sheet made for this file last time, so re-opening the same CSV
    // refreshes one spreadsheet instead of piling up copies.
    var id = req.fileId ? updateInPlace(req.fileId, blob) : null;
    if (!id) id = createSheet(name, blob);

    return reply({ id: id, url: 'https://docs.google.com/spreadsheets/d/' + id + '/edit' });
  } catch (err) {
    return reply({ error: String((err && err.message) || err) });
  }
}

/** Returns the fileId on success, or null if the caller should get a fresh sheet. */
function updateInPlace(fileId, blob) {
  var file;
  try {
    file = DriveApp.getFileById(fileId);
    if (file.isTrashed()) return null;
  } catch (err) {
    return null; // deleted, or never ours
  }

  // Refuse to touch anything outside this script's own folder. Without this check a
  // caller holding the secret could overwrite or bin any file in the account just by
  // naming its id.
  if (!isInAppFolder(file)) return null;

  try {
    Drive.Files.update({ mimeType: MimeType.GOOGLE_SHEETS }, fileId, blob);
  } catch (err) {
    return null;
  }

  // If the re-import quietly demoted it from a Sheet, bin it and start over.
  if (DriveApp.getFileById(fileId).getMimeType() !== MimeType.GOOGLE_SHEETS) {
    file.setTrashed(true);
    return null;
  }
  return fileId;
}

function isInAppFolder(file) {
  var wanted = getFolder().getId();
  var parents = file.getParents();
  while (parents.hasNext()) {
    if (parents.next().getId() === wanted) return true;
  }
  return false;
}

function createSheet(name, blob) {
  var folderId = getFolder().getId();
  if (Drive.Files.create) {
    // Drive advanced service v3
    return Drive.Files.create(
      { name: name, mimeType: MimeType.GOOGLE_SHEETS, parents: [folderId] }, blob).id;
  }
  // Drive advanced service v2
  return Drive.Files.insert(
    { title: name, mimeType: MimeType.GOOGLE_SHEETS, parents: [{ id: folderId }] }, blob).id;
}

function getFolder() {
  var it = DriveApp.getFoldersByName(FOLDER_NAME);
  return it.hasNext() ? it.next() : DriveApp.createFolder(FOLDER_NAME);
}

function reply(obj) {
  return ContentService.createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
