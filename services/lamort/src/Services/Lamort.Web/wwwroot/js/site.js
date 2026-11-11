document.addEventListener("click", function (event) {
  var addButton = event.target.closest("[data-add-row]");
  if (addButton) {
    var templateId = addButton.getAttribute("data-add-row");
    var targetId = addButton.getAttribute("data-target");
    var template = document.getElementById(templateId);
    var target = document.getElementById(targetId);

    if (!template || !target) {
      return;
    }

    var index = target.querySelectorAll(".entry-row").length;
    var html = template.innerHTML.replaceAll("__index__", index.toString());
    target.insertAdjacentHTML("beforeend", html);
    return;
  }

  var removeButton = event.target.closest("[data-remove-row]");
  if (removeButton) {
    var row = removeButton.closest(".entry-row");
    var container = row?.parentElement;
    if (!row || !container) {
      return;
    }

    row.remove();

    if (container.querySelectorAll(".entry-row").length === 0) {
      var fallbackButton = document.querySelector('[data-target="' + container.id + '"]');
      fallbackButton?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    }
  }

  var pdfButton = event.target.closest("[data-open-pdf]");
  if (pdfButton) {
    event.preventDefault();

    var mortgageId = pdfButton.getAttribute("data-open-pdf");
    if (!mortgageId) {
      return;
    }

    fetch("/auth/token", { credentials: "same-origin" })
      .then(function (response) {
        if (!response.ok) {
          throw new Error("Unable to get token.");
        }

        return response.json();
      })
      .then(function (payload) {
        var accessToken = payload && payload.accessToken;
        if (!accessToken) {
          throw new Error("Missing token.");
        }

        window.open("/docrender/api/mortgages/" + mortgageId + "/pdf?access_token=" + encodeURIComponent(accessToken), "_blank", "noopener");
      })
      .catch(function () {
        window.location.href = "/App";
      });
  }
});
