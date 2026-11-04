(function ($) {
    if (!$.validator) {
        return;
    }

    $.validator.addMethod("notEqualToField", function (value, element, selector) {
        var otherValue = $(selector).val();
        return this.optional(element) || !value || !otherValue || value !== otherValue;
    });

    $.validator.addMethod("greaterThanField", function (value, element, selector) {
        var otherValue = $(selector).val();
        return this.optional(element) || !value || !otherValue || value > otherValue;
    });

    $.validator.addMethod("notPastDate", function (value, element) {
        if (this.optional(element) || !value) {
            return true;
        }
        var today = new Date();
        var minDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        return new Date(value + "T00:00:00") >= minDate;
    });

    $.validator.addMethod("ecuadorianCedula", function (value, element) {
        var $card = $(element).closest(".card");
        if ($card.find("[name='tipoDocumento']").val() !== "CEDULA") {
            return true;
        }
        if (!/^\d{10}$/.test(value)) {
            return false;
        }

        var provincia = parseInt(value.substring(0, 2), 10);
        if (provincia < 1 || provincia > 24 || parseInt(value.charAt(2), 10) > 5) {
            return false;
        }

        var suma = 0;
        for (var i = 0; i < 9; i++) {
            var digito = parseInt(value.charAt(i), 10);
            if (i % 2 === 0) {
                digito *= 2;
                if (digito > 9) { digito -= 9; }
            }
            suma += digito;
        }
        return (10 - (suma % 10)) % 10 === parseInt(value.charAt(9), 10);
    }, "Ingrese una cédula ecuatoriana válida.");

    $(function () {
        $("form[data-validate-route]").each(function () {
            var $form = $(this);
            $form.find("[name='AeropuertoDestinoId']").rules("add", {
                notEqualToField: "#AeropuertoOrigenId",
                messages: { notEqualToField: "El origen y el destino deben ser distintos." }
            });
        });

        $("form[data-validate-frequency]").each(function () {
            var $form = $(this);
            $form.find("[name='HoraLlegada']").rules("add", {
                greaterThanField: "#HoraSalida",
                messages: { greaterThanField: "La hora de llegada debe ser posterior a la de salida." }
            });
        });

        $("form[data-validate-search]").each(function () {
            var $form = $(this);
            $form.find("[name='Destino']").rules("add", {
                notEqualToField: "#Origen",
                messages: { notEqualToField: "El origen y el destino deben ser distintos." }
            });
            $form.find("[name='Fecha']").rules("add", {
                notPastDate: true,
                messages: { notPastDate: "La fecha no puede ser anterior a hoy." }
            });
            $form.find("[name='Pasajeros']").rules("add", {
                required: true,
                range: [1, 7],
                messages: { required: "Indique la cantidad de pasajeros.", range: "Debe indicar entre 1 y 7 pasajeros." }
            });
        });

        $("form[data-validate-passengers]").each(function () {
            $(this).find("[name='numeroDocumento']").each(function () {
                $(this).rules("add", {
                    ecuadorianCedula: true,
                    messages: { ecuadorianCedula: "Ingrese una cédula ecuatoriana válida." }
                });
            });
        });
    });
})(jQuery);
