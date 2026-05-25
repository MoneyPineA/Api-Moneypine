namespace ApiEjemplo.DTOs.Cliente
{
    public class ClienteDTO
    {
        public int cliente_id { get; set; }
        public string? clave_cliente { get; set; }
        public int usuario_id { get; set; }

        public string? tipo_cliente        { get; set; }
        public string? ruta_vinculacion    { get; set; }
        public bool permitir_acceso_web    { get; set; }

        public string? nombre_usuario      { get; set; }
        public string? apellido_usuario    { get; set; }
        public string? apellido_materno    { get; set; }
        public string? correo_usuario      { get; set; }
        public string? telefono_usuario    { get; set; }
        public string? estado_usuario      { get; set; } // MONEYPINE-FIX: estado del usuario (ACTIVO/INACTIVO/BLOQUEADO)

        public string? sexo                { get; set; }
        public string? estado_civil        { get; set; }
        public string? lugar_nacimiento    { get; set; }
        public int? no_dependientes        { get; set; }
        public string? telefono_oficina    { get; set; }
        public string? telefono_particular { get; set; }

        public string? direccion           { get; set; }
        public string? colonia             { get; set; }
        public DateTime? fecha_nacimiento  { get; set; }
        public string? curp                { get; set; }
        public string? rfc                 { get; set; }

        public string? empresa_nombre      { get; set; }
        public string? empresa_rfc         { get; set; }
        public string? empresa_correo      { get; set; }
        public string? empresa_telefono_oficina    { get; set; }
        public string? empresa_telefono_particular { get; set; }
        public string? empresa_telefono_celular    { get; set; }
    }
}