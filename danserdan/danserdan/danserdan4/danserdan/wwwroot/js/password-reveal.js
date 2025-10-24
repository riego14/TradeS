(function (global, $) {
    if (!$) {
        return;
    }

    var NAMESPACE = 'TradeSPasswordReveal';
    var BULLET_CHAR = '\u2022';
    var MASK_DELAY = 1000; // milliseconds

    if (!global[NAMESPACE]) {
        global[NAMESPACE] = {};
    }

    if (typeof global[NAMESPACE].init === 'function') {
        return;
    }

    global[NAMESPACE].init = function (context) {
        context = context || $(document);

        context.find('.password-reveal-field').each(function () {
            var $field = $(this);

            if ($field.data('passwordRevealInit')) {
                return;
            }

            $field.data('passwordRevealInit', true);

            var $input = $field.find('input[data-password-input]');
            var $overlay = $field.find('.password-overlay');
            var $toggle = $field.find('.password-toggle');
            var maskTimeoutId = null;
            var placeholder = $input.attr('placeholder') || '';

            if (!$input.length) {
                return;
            }

            if (!$overlay.length) {
                $overlay = $('<span class="password-overlay empty" aria-hidden="true"></span>');
                $field.append($overlay);
            }

            $field.addClass('password-reveal-ready');

            function setOverlayText(text) {
                if (!$overlay.length) {
                    return;
                }

                if (text) {
                    $overlay.text(text).removeClass('empty');
                } else {
                    $overlay.text(placeholder).addClass('empty');
                }
            }

            function buildMaskedValue(revealLast) {
                var value = $input.val() || '';

                if (!value) {
                    setOverlayText('');
                    return;
                }

                var output = '';

                for (var i = 0; i < value.length; i++) {
                    if (revealLast && i === value.length - 1) {
                        output += value.charAt(i);
                    } else {
                        output += BULLET_CHAR;
                    }
                }

                setOverlayText(output);
            }

            function clearMaskTimer() {
                if (maskTimeoutId) {
                    clearTimeout(maskTimeoutId);
                    maskTimeoutId = null;
                }
            }

            function maskAll() {
                if ($field.hasClass('password-showing')) {
                    return;
                }

                clearMaskTimer();
                buildMaskedValue(false);
            }

            function revealLastCharacterTemporarily() {
                if ($field.hasClass('password-showing')) {
                    return;
                }

                clearMaskTimer();
                buildMaskedValue(true);

                maskTimeoutId = setTimeout(function () {
                    maskTimeoutId = null;
                    buildMaskedValue(false);
                }, MASK_DELAY);
            }

            $input.on('input', function (e) {
                if ($field.hasClass('password-showing')) {
                    return;
                }

                var originalEvent = e.originalEvent || {};
                var inputType = originalEvent.inputType || '';

                if (inputType && inputType.indexOf('delete') === 0) {
                    maskAll();
                } else {
                    revealLastCharacterTemporarily();
                }
            });

            $input.on('blur', maskAll);

            $input.on('focus', function () {
                if ($field.hasClass('password-showing')) {
                    return;
                }

                buildMaskedValue(false);
            });

            if ($toggle.length) {
                $toggle.on('click', function (event) {
                    event.preventDefault();
                    var showing = $field.toggleClass('password-showing').hasClass('password-showing');

                    clearMaskTimer();

                    if (showing) {
                        setOverlayText('');
                        $input.attr('type', 'text');
                        $toggle.attr('aria-label', 'Hide password');
                        $toggle.html('<i class="bi bi-eye-slash"></i>');
                    } else {
                        $input.attr('type', 'password');
                        maskAll();
                        $toggle.attr('aria-label', 'Show password');
                        $toggle.html('<i class="bi bi-eye"></i>');
                    }

                    // Keep focus on the input for smoother UX
                    $input.trigger('focus');
                });
            }

            // Initialize overlay with current value (e.g., autofill)
            maskAll();
            setTimeout(maskAll, 300);
            setTimeout(maskAll, 1500);
        });
    };
})(window, window.jQuery);
