require 'sketchup.rb'
require 'fileutils'
require 'json'
require 'securerandom'

module ArchiTecStudio
  def self.execute(req)
    model = Sketchup.active_model
    doc = "#{Process.pid}:#{model.object_id}:#{model.path}"
    args = req.fetch('args')
    return {document_id: doc, version: Sketchup.version, selected: model.selection.map(&:persistent_id).first(100), units: 'inches'} if req['command'] == 'get_context'
    command = req.fetch('command')
    if %w[save_copy export_obj render_png].include?(command)
      raise 'Active document changed' unless args['document_id'] == doc
      uuid = args.fetch('request_id')
      raise 'Invalid request ID' unless /\A[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\z/i.match?(uuid)
      key = [doc, uuid, command]
      return @receipts[key] if @receipts.key?(key)
      folder = File.join(ENV.fetch('LOCALAPPDATA'),'AIArchiTec','Exports',uuid)
      FileUtils.mkdir_p(folder)
      path = File.join(folder,{'save_copy'=>'model.skp','export_obj'=>'model.obj','render_png'=>'view.png'}.fetch(command))
      raise 'Output already exists' if File.exist?(path)
      ok = case command
           when 'save_copy' then model.save_copy(path)
           when 'export_obj' then model.export(path,false)
           else model.active_view.write_image(path)
           end
      raise 'Host could not write the output' unless ok && File.size?(path)
      return @receipts[key] = {path: path, bytes: File.size(path)}
    end
    raise 'Inspect the active model first.' unless req['command'] == 'create_mesh' && args['document_id'] == doc
    key = [doc,args.fetch('request_id')]
    return @receipts[key] if @receipts.key?(key)
    verts = args.fetch('vertices')
    faces = args.fetch('faces')
    raise 'Invalid mesh' unless (3..20000).cover?(verts.size) && (1..30000).cover?(faces.size)
    points = verts.map do |v|
      raise 'Invalid vertex' unless v.size == 3 && v.all?{|x| x.is_a?(Numeric) && x.abs < 1e9}
      Geom::Point3d.new(*v)
    end
    model.start_operation('AI Archi-Tec · Mesh', true)
    begin
      group = model.entities.add_group
      group.name = args.fetch('name').to_s[0,120]
      faces.each do |f|
        raise 'Invalid triangle' unless f.size == 3 && f.uniq.size == 3 && f.all?{|i| i.is_a?(Integer) && i.between?(0,points.size-1)}
        raise 'Degenerate triangle' unless group.entities.add_face(f.map{|i| points[i]})
      end
      result = {created: group.name,id: group.persistent_id,triangles: faces.size}
      model.commit_operation
      @receipts[key] = result
    rescue StandardError
      model.abort_operation
      raise
    end
  end
  UI.stop_timer(@timer) if @timer
  @receipts = {}
  root = File.join(ENV.fetch('LOCALAPPDATA'),'AIArchiTec','Sessions','SketchUp',Process.pid.to_s)
  FileUtils.mkdir_p(root)
  File.write(File.join(root,'host.json'),JSON.generate({pid: Process.pid,version: (2000+Sketchup.version.to_i).to_s}))
  @timer = UI.start_timer(0.35,true) do
    FileUtils.touch(File.join(root,'heartbeat'))
    Dir.glob(File.join(root,'*.req')).first(4).each do |path|
      next if File.exist?(path.sub(/\.req\z/,".out"))
      begin
        req = JSON.parse(File.binread(path).force_encoding('UTF-8').sub(/\A\uFEFF/,''))
        raise 'Expired request' if req.fetch('expires') < Time.now.to_i
        result = execute(req)
      rescue StandardError => e
        result = {error: e.message}
      end
      pending = path.sub(/\.req\z/,'.pending')
      File.write(pending,JSON.generate(result))
      File.rename(pending,path.sub(/\.req\z/,'.out'))
      FileUtils.rm_f(path)
    end
  end
end
