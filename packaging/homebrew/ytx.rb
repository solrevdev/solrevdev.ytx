# Template for Formula/ytx.rb in the solrevdev/homebrew-tap repository.
# Install with: brew install solrevdev/tap/ytx
#
# Replace the @@...@@ placeholders from the release's SHA256SUMS (see packaging/README.md).
# Unrelated project with the same command name: koguchic/ytx (Rust, `cargo install ytx-cli`).
# The fully qualified tap name avoids a formula clash, but both install a `ytx` command.
class Ytx < Formula
  desc "Extract YouTube title, description and transcript (raw + Markdown) as JSON"
  homepage "https://github.com/solrevdev/solrevdev.ytx"
  version "@@VERSION@@"
  license "MIT"

  on_macos do
    on_arm do
      url "https://github.com/solrevdev/solrevdev.ytx/releases/download/v#{version}/ytx-#{version}-osx-arm64.tar.gz"
      sha256 "@@SHA256_OSX_ARM64@@"
    end
    on_intel do
      url "https://github.com/solrevdev/solrevdev.ytx/releases/download/v#{version}/ytx-#{version}-osx-x64.tar.gz"
      sha256 "@@SHA256_OSX_X64@@"
    end
  end

  on_linux do
    on_arm do
      url "https://github.com/solrevdev/solrevdev.ytx/releases/download/v#{version}/ytx-#{version}-linux-arm64.tar.gz"
      sha256 "@@SHA256_LINUX_ARM64@@"
    end
    on_intel do
      url "https://github.com/solrevdev/solrevdev.ytx/releases/download/v#{version}/ytx-#{version}-linux-x64.tar.gz"
      sha256 "@@SHA256_LINUX_X64@@"
    end
    # The .NET HTTP stack loads the system OpenSSL 3 (libssl.so.3) at run time and needs CA certificates.
    # Most desktop distributions have both. A minimal container may not.
  end

  def install
    bin.install "ytx"
  end

  test do
    assert_match "ytx #{version}", shell_output("#{bin}/ytx --version")
    # Offline usage error: invalid input must exit 2 without touching the network.
    shell_output("#{bin}/ytx 'not a video!!' 2>&1", 2)
  end
end
